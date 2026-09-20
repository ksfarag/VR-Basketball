using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VRBasketball.EditorAutomation
{
    /// <summary>
    /// Builds the court and hoop in the open scene from <see cref="CourtSettings"/>.
    ///
    /// The ring is a loop of capsules rather than a mesh collider: capsules are primitives,
    /// so a fast ball gets exact contacts and continuous collision has something simple to
    /// sweep against, where a thin torus mesh gives poor normals and invites tunnelling.
    /// Everything it makes lives under one "Court" root and is regenerated wholesale, so
    /// changing a dimension means editing the asset and running this again.
    ///
    /// The scoring parts are built here too, for the same reason: the sensor's plane is the
    /// ring's plane and the readout hangs off the backboard, so both are dimensions of the
    /// hoop. Wiring them here is also what keeps them alive, since a rebuild destroys
    /// everything under the root and anything attached by hand would go with it.
    /// </summary>
    public static class CourtBuilder
    {
        private const string RootName = "Court";
        private const string ScoringRootName = "Scoring";
        private const string RingMeshPath = "Assets/Meshes/Ring.mesh";
        private const int RingMeshTubeSides = 8;

        // The readout. Digits are sized to be read from the far end of the court rather
        // than to match anything on a real board.
        private const int ScoreDigits = 3;
        private const float DigitHeight = 0.34f;
        private const float DigitWidth = 0.2f;
        private const float DigitBar = 0.04f;
        private const float DigitDepth = 0.04f;
        private const float DigitGap = 0.06f;
        private const float PanelMargin = 0.09f;
        private const float PanelDepth = 0.06f;

        [MenuItem("VR Basketball/Court/Rebuild Court")]
        public static void Rebuild()
        {
            CourtSettings settings = LoadSettings();
            if (settings == null)
            {
                Debug.LogError("No CourtSettings asset found. Create one from Assets > Create > VR Basketball > Court Settings.");
                return;
            }

            Transform root = FindOrCreateRoot();
            Undo.RegisterFullObjectHierarchyUndo(root.gameObject, "Rebuild Court");

            for (int i = root.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(root.GetChild(i).gameObject);

            PhysicsMaterial courtSurface = LoadOrCreatePhysicsMaterial("Court", 0.6f, 0.6f, 1f);
            PhysicsMaterial boardSurface = LoadOrCreatePhysicsMaterial("Backboard", 0.4f, 0.4f, 0.8f);
            PhysicsMaterial ringSurface = LoadOrCreatePhysicsMaterial("Ring", 0.5f, 0.5f, 0.55f);
            // The ball's own surface is made here so the set stays consistent, but it is
            // assigned on the ball rather than by the court.
            LoadOrCreatePhysicsMaterial("Ball", 0.6f, 0.6f, 0.85f);

            // Found rather than made where one already exists, so a score keeper the
            // developer has tuned survives a rebuild that replaces the hoop under it.
            ScoreKeeper score = FindOrCreateScoreKeeper();
            Undo.RecordObject(score, "Rebuild Court");

            BuildFloor(root, settings, courtSurface);
            BuildBounds(root, settings, courtSurface);
            BuildHoop(root, settings, boardSurface, ringSurface, score);

            EditorUtility.SetDirty(score);
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);

            float narrowest = CourtLayout.NarrowestHoleRadius(settings.RimSegments, settings.RimInnerRadius, settings.RimTubeRadius);
            Debug.Log("Court rebuilt: ring hole " + (settings.RimInnerRadius * 2f).ToString("F3") + " m across at "
                      + settings.RimHeight.ToString("F3") + " m, narrowest " + (narrowest * 2f).ToString("F3") + " m.");
        }

        private static void BuildFloor(Transform root, CourtSettings s, PhysicsMaterial surface)
        {
            float length = s.FloorBackEdge - s.FloorFrontEdge;
            GameObject floor = Box("Floor", root,
                new Vector3(0f, -s.FloorThickness * 0.5f, (s.FloorBackEdge + s.FloorFrontEdge) * 0.5f),
                new Vector3(s.CourtWidth, s.FloorThickness, length),
                Surface("Court", new Color(0.62f, 0.44f, 0.26f)));
            floor.GetComponent<BoxCollider>().sharedMaterial = surface;
        }

        private static void BuildBounds(Transform root, CourtSettings s, PhysicsMaterial surface)
        {
            if (s.WallHeight <= 0f)
                return;

            GameObject bounds = Child("Bounds (temporary; recovery is plan item 5)", root, Vector3.zero);
            float back = s.FloorBackEdge;
            float front = s.FloorFrontEdge;
            float length = back - front;
            float middle = (back + front) * 0.5f;
            float half = s.CourtWidth * 0.5f;
            float y = s.WallHeight * 0.5f;
            float t = s.WallThickness;
            Material material = Surface("CourtWall", new Color(0.24f, 0.26f, 0.30f));

            Wall("Wall Behind Hoop", bounds.transform, new Vector3(0f, y, back + t * 0.5f), new Vector3(s.CourtWidth + t * 2f, s.WallHeight, t), material, surface);
            Wall("Wall Far", bounds.transform, new Vector3(0f, y, front - t * 0.5f), new Vector3(s.CourtWidth + t * 2f, s.WallHeight, t), material, surface);
            Wall("Wall Left", bounds.transform, new Vector3(-half - t * 0.5f, y, middle), new Vector3(t, s.WallHeight, length), material, surface);
            Wall("Wall Right", bounds.transform, new Vector3(half + t * 0.5f, y, middle), new Vector3(t, s.WallHeight, length), material, surface);
        }

        private static void Wall(string name, Transform parent, Vector3 position, Vector3 size, Material material, PhysicsMaterial surface)
        {
            GameObject go = Box(name, parent, position, size, material);
            go.GetComponent<BoxCollider>().sharedMaterial = surface;
        }

        private static void BuildHoop(Transform root, CourtSettings s, PhysicsMaterial board, PhysicsMaterial ring, ScoreKeeper score)
        {
            GameObject hoop = Child("Hoop", root, Vector3.zero);

            GameObject backboard = Box("Backboard", hoop.transform,
                new Vector3(0f, s.BackboardBottomHeight + s.BackboardHeight * 0.5f, s.RimCentreToBackboardFace + s.BackboardThickness * 0.5f),
                new Vector3(s.BackboardWidth, s.BackboardHeight, s.BackboardThickness),
                Surface("Backboard", new Color(0.88f, 0.88f, 0.90f)));
            backboard.GetComponent<BoxCollider>().sharedMaterial = board;

            BasketSensor sensor = BuildRing(hoop.transform, s, ring);
            BuildPost(hoop.transform, s, board);
            BuildScoreboard(hoop.transform, s, score);

            score.Sensors = new[] { sensor };
        }

        private static BasketSensor BuildRing(Transform hoop, CourtSettings s, PhysicsMaterial surface)
        {
            GameObject ring = Child("Ring", hoop, new Vector3(0f, s.RimCentreHeight, 0f));
            Material material = Surface("Ring", new Color(0.86f, 0.31f, 0.09f));

            GameObject visual = Child("Ring Mesh", ring.transform, Vector3.zero);
            visual.AddComponent<MeshFilter>().sharedMesh = SaveRingMesh(s);
            visual.AddComponent<MeshRenderer>().sharedMaterial = material;

            for (int i = 0; i < s.RimSegments; i++)
            {
                CourtLayout.RingSegment segment = CourtLayout.Segment(i, s.RimSegments, s.RimCentrelineRadius, s.RimTubeRadius);
                GameObject go = Child("Segment " + i.ToString("00"), ring.transform, segment.Position);
                go.transform.localRotation = segment.Rotation;

                CapsuleCollider capsule = go.AddComponent<CapsuleCollider>();
                capsule.direction = 2; // runs along the object's forward, which points down the chord
                capsule.radius = s.RimTubeRadius;
                capsule.height = segment.Height;
                capsule.sharedMaterial = surface;
            }

            // The sensor's own transform is its plane, and this object is already exactly
            // the ring: centred on the hole, at the height of the tube, level with it. The
            // arming height is left at the component's own default, so that number has one
            // home; only the hole comes from the dimensions the ring was built to.
            BasketSensor sensor = ring.AddComponent<BasketSensor>();
            sensor.PassRadius = s.RimInnerRadius;
            return sensor;
        }

        /// <summary>
        /// A seven-segment readout resting on top of the backboard, where it is in the
        /// shooter's view of the hoop rather than somewhere they have to look away to.
        /// </summary>
        private static void BuildScoreboard(Transform hoop, CourtSettings s, ScoreKeeper score)
        {
            float width = ScoreDigits * DigitWidth + (ScoreDigits - 1) * DigitGap + PanelMargin * 2f;
            float height = DigitHeight + PanelMargin * 2f;

            GameObject board = Child("Scoreboard", hoop,
                new Vector3(0f, s.BackboardBottomHeight + s.BackboardHeight + height * 0.5f,
                    s.RimCentreToBackboardFace + s.BackboardThickness * 0.5f));

            Box("Panel", board.transform, Vector3.zero, new Vector3(width, height, PanelDepth),
                Surface("Scoreboard", new Color(0.05f, 0.06f, 0.08f)), false);

            // The readout swells for a moment on a basket, so it must not be batched into
            // the scenery, and its bars change material, so they must not bake their light
            // into it either.
            GameObject readout = Child("Readout", board.transform,
                new Vector3(0f, 0f, -(PanelDepth + DigitDepth) * 0.5f));
            Dynamic(readout);

            Material lit = Glow("ScoreDigit", new Color(1f, 0.55f, 0.12f));
            Material dim = Surface("ScoreDigitOff", new Color(0.08f, 0.08f, 0.09f));
            Material flash = Glow("ScoreDigitFlash", new Color(0.65f, 1f, 0.72f));

            var digits = new Scoreboard.Digit[ScoreDigits];
            for (int cell = 0; cell < ScoreDigits; cell++)
            {
                // Cell 0 is the most significant, and the player reads the board from the
                // near side of the court, where local +x is their right.
                float x = (cell - (ScoreDigits - 1) * 0.5f) * (DigitWidth + DigitGap);
                GameObject digit = Child("Digit " + cell, readout.transform, new Vector3(x, 0f, 0f));
                Dynamic(digit);

                var segments = new Renderer[SevenSegment.Count];
                for (int i = 0; i < segments.Length; i++)
                {
                    SevenSegment.Bar bar = SevenSegment.Layout(i, DigitWidth, DigitHeight, DigitBar, DigitDepth);
                    GameObject go = Box("Segment " + i, digit.transform, bar.Position, bar.Size, dim, false);
                    Dynamic(go);
                    segments[i] = go.GetComponent<MeshRenderer>();
                }

                digits[cell] = new Scoreboard.Digit { Segments = segments };
            }

            AudioSource speaker = board.AddComponent<AudioSource>();
            speaker.playOnAwake = false;
            // Flat rather than positioned: the board is across the court and a basket has
            // to be heard wherever the player is standing and whichever way they face.
            speaker.spatialBlend = 0f;

            Scoreboard display = readout.AddComponent<Scoreboard>();
            display.Score = score;
            display.Digits = digits;
            display.Speaker = speaker;
            display.SetMaterials(lit, dim, flash);
        }

        private static void BuildPost(Transform hoop, CourtSettings s, PhysicsMaterial surface)
        {
            // The post stands clear behind the board so it is never in the way of a shot.
            const float Clearance = 0.6f;
            const float Thickness = 0.16f;

            GameObject post = Child("Post", hoop, Vector3.zero);
            Material material = Surface("Post", new Color(0.20f, 0.21f, 0.24f));

            float behindBoard = s.RimCentreToBackboardFace + s.BackboardThickness;
            float top = s.BackboardBottomHeight;

            GameObject column = Box("Column", post.transform,
                new Vector3(0f, top * 0.5f, behindBoard + Clearance + Thickness * 0.5f),
                new Vector3(Thickness, top, Thickness), material);
            column.GetComponent<BoxCollider>().sharedMaterial = surface;

            float armLength = Clearance + Thickness;
            GameObject arm = Box("Arm", post.transform,
                new Vector3(0f, top - Thickness * 0.5f, behindBoard + armLength * 0.5f),
                new Vector3(Thickness, Thickness, armLength), material);
            arm.GetComponent<BoxCollider>().sharedMaterial = surface;
        }

        private static GameObject Child(string name, Transform parent, Vector3 localPosition)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Rebuild Court");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            GameObjectUtility.SetStaticEditorFlags(go, (StaticEditorFlags)~0);
            return go;
        }

        private static GameObject Box(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material, bool collider = true)
        {
            GameObject go = Child(name, parent, localPosition);
            go.AddComponent<MeshFilter>().sharedMesh = PrimitiveMesh(PrimitiveType.Cube);
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (collider)
                go.AddComponent<BoxCollider>();
            go.transform.localScale = size;
            return go;
        }

        /// <summary>Takes back the static flags <see cref="Child"/> sets, for something that moves or changes.</summary>
        private static void Dynamic(GameObject go)
        {
            GameObjectUtility.SetStaticEditorFlags(go, 0);
        }

        private static Mesh PrimitiveMesh(PrimitiveType type)
        {
            GameObject temp = GameObject.CreatePrimitive(type);
            Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temp);
            return mesh;
        }

        /// <summary>
        /// A torus matching the capsule ring, written to an asset so the scene keeps a real
        /// reference rather than a mesh that only lives until the next reload.
        /// </summary>
        private static Mesh SaveRingMesh(CourtSettings s)
        {
            Mesh mesh = BuildRingMesh(s.RimCentrelineRadius, s.RimTubeRadius, s.RimSegments, RingMeshTubeSides);
            Directory.CreateDirectory(Path.GetDirectoryName(RingMeshPath));

            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(RingMeshPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, RingMeshPath);
                return mesh;
            }

            // Overwrite in place so references to it survive the rebuild.
            existing.Clear();
            existing.vertices = mesh.vertices;
            existing.normals = mesh.normals;
            existing.uv = mesh.uv;
            existing.triangles = mesh.triangles;
            existing.RecalculateBounds();
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssets();
            return existing;
        }

        private static Mesh BuildRingMesh(float centrelineRadius, float tubeRadius, int around, int tubeSides)
        {
            var vertices = new Vector3[(around + 1) * (tubeSides + 1)];
            var normals = new Vector3[vertices.Length];
            var uv = new Vector2[vertices.Length];

            for (int i = 0; i <= around; i++)
            {
                float u = (float)i / around * Mathf.PI * 2f;
                Vector3 outward = new Vector3(Mathf.Cos(u), 0f, Mathf.Sin(u));
                Vector3 centre = outward * centrelineRadius;

                for (int j = 0; j <= tubeSides; j++)
                {
                    float v = (float)j / tubeSides * Mathf.PI * 2f;
                    Vector3 normal = outward * Mathf.Cos(v) + Vector3.up * Mathf.Sin(v);
                    int k = i * (tubeSides + 1) + j;
                    vertices[k] = centre + normal * tubeRadius;
                    normals[k] = normal;
                    uv[k] = new Vector2((float)i / around, (float)j / tubeSides);
                }
            }

            var triangles = new int[around * tubeSides * 6];
            int t = 0;
            for (int i = 0; i < around; i++)
            {
                for (int j = 0; j < tubeSides; j++)
                {
                    int a = i * (tubeSides + 1) + j;
                    int b = (i + 1) * (tubeSides + 1) + j;
                    triangles[t++] = a; triangles[t++] = a + 1; triangles[t++] = b;
                    triangles[t++] = b; triangles[t++] = a + 1; triangles[t++] = b + 1;
                }
            }

            var mesh = new Mesh { name = "Ring" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Transform FindOrCreateRoot()
        {
            GameObject existing = GameObject.Find(RootName);
            if (existing != null)
                return existing.transform;

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Rebuild Court");
            return root.transform;
        }

        private static ScoreKeeper FindOrCreateScoreKeeper()
        {
            ScoreKeeper existing = Object.FindFirstObjectByType<ScoreKeeper>(FindObjectsInactive.Include);
            if (existing != null)
                return existing;

            var go = new GameObject(ScoringRootName);
            Undo.RegisterCreatedObjectUndo(go, "Rebuild Court");
            return Undo.AddComponent<ScoreKeeper>(go);
        }

        private static CourtSettings LoadSettings()
        {
            string[] guids = AssetDatabase.FindAssets("t:CourtSettings");
            return guids.Length == 0 ? null : AssetDatabase.LoadAssetAtPath<CourtSettings>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private static Material Surface(string name, Color colour)
        {
            string path = "Assets/Materials/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", 0.2f);
            Directory.CreateDirectory("Assets/Materials");
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>
        /// A material for a bar of the readout that is lit. Unlit rather than emissive: a
        /// lit material's emission depends on a shader keyword that does not survive being
        /// written to an asset here, and the first attempt shipped a board whose lit bars
        /// were a dull brown. Unlit renders the colour asked for whatever the lighting is
        /// doing, which is what a display is supposed to look like, and costs the headset
        /// less than lighting a bar whose brightness is not meant to come from the scene.
        /// </summary>
        private static Material Glow(string name, Color colour)
        {
            string path = "Assets/Materials/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name };
            material.SetColor("_BaseColor", colour);
            Directory.CreateDirectory("Assets/Materials");
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static PhysicsMaterial LoadOrCreatePhysicsMaterial(string name, float dynamicFriction, float staticFriction, float bounciness)
        {
            string path = "Assets/Physics/" + name + ".asset";
            PhysicsMaterial existing = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (existing != null)
                return existing;

            var material = new PhysicsMaterial(name)
            {
                dynamicFriction = dynamicFriction,
                staticFriction = staticFriction,
                bounciness = bounciness,
                frictionCombine = PhysicsMaterialCombine.Average,
                // Multiply on both sides makes the ball's own bounciness the one that
                // matters and each surface a fraction of it, so feel is tuned in one place.
                bounceCombine = PhysicsMaterialCombine.Multiply
            };
            Directory.CreateDirectory("Assets/Physics");
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
