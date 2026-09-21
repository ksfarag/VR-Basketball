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
        private const string CourtSurfaceMeshPath = "Assets/Meshes/CourtSurface.mesh";
        private const int RingMeshTubeSides = 8;

        // The arena pack is the one place the court's look is defined, so its materials are
        // loaded by path rather than copied into this project's own folder.
        private const string PackMaterials = "Assets/MarpaStudio/Built-In/Materials/";
        private const string ArenaHoopPrefab = "Assets/MarpaStudio/Built-In/Prefabs/Ring.prefab";
        private const string ArenaNetPrefab = "Assets/MarpaStudio/Built-In/Prefabs/Net.prefab";

        // Where the pack's hoop keeps its parts, measured off its mesh rather than guessed.
        // Its rim sits at this offset from the prefab's own origin and the hoop faces its
        // local -x, so the visual is placed by putting that point on the ring this court
        // builds and turning it to face the player. CourtSettings carries the same figures,
        // so the colliders built here land inside the visual instead of near it.
        private static readonly Vector3 ArenaRimOffset = new Vector3(-1.9292f, 2.8485f, 0.0008f);
        private static readonly Quaternion ArenaFacing = Quaternion.Euler(0f, -90f, 0f);

        /// <summary>Centre of the pack's stanchion, measured back from the rim's centre.</summary>
        private const float ArenaPoleBack = 1.9292f;
        private const float ArenaPoleWidth = 0.30f;
        private const float ArenaPoleTop = 2.55f;

        /// <summary>Near end of the arm that carries the board, measured back from the rim's centre.</summary>
        private const float ArenaArmNear = 0.479f;

        /// <summary>The net's own top ring, in its local space, and the rim it was modelled around.</summary>
        private const float ArenaNetTop = 0.429f;
        private const float ArenaNetRadius = 0.4402f;

        // The pack's court sheet is a single quad carrying a whole 28.65 m court in its
        // texture. This court is a half court, so the markings are read out of that atlas
        // rather than stretched to fit: the baseline lands on the texture's baseline and
        // the far edge falls wherever the court's length reaches along a real one. Getting
        // this wrong scales the key and the arc, which is exactly what a shooter judges
        // distance by, so the numbers are the pack's own and are not adjustable.
        private const float PackCourtLength = 28.65f;
        private const float PackUMin = 0.001f;
        private const float PackUMax = 0.532f;
        private const float PackVMin = 0.001f;
        private const float PackVMax = 0.999f;

        // Behind the baseline there is no court to draw, and the pack has no plain boards
        // outside the lines, so the run-off borrows a clean band from inside them: the strip
        // between the centre circle and the top of the arc carries no markings at any width.
        private const float PackWoodV = 0.60f;

        /// <summary>How far the markings sit above the floor, to keep them off its surface.</summary>
        private const float MarkingsLift = 0.002f;

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
            BuildCourtSurface(root, settings);
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

        /// <summary>
        /// The painted markings, as a quad laid over the floor. Separate from the floor slab
        /// because the slab is a box whose sides show at the court's edge and whose top face
        /// would stretch one copy of the texture over the whole court; this carries its own
        /// mapping and covers only the playing area, leaving the run-off behind the baseline
        /// as bare boards, which is what is actually under a hoop.
        ///
        /// It has no collider. The floor slab underneath is still what the ball bounces on,
        /// so the tuned surface and its physics material are untouched by anything here.
        /// </summary>
        private static void BuildCourtSurface(Transform root, CourtSettings s)
        {
            Material markings = PackMaterial("PlayField");
            if (markings == null)
                return;

            GameObject surface = Child("Court Surface", root, new Vector3(0f, MarkingsLift, 0f));
            surface.AddComponent<MeshFilter>().sharedMesh = SaveMesh(BuildCourtSurfaceMesh(s), CourtSurfaceMeshPath);
            surface.AddComponent<MeshRenderer>().sharedMaterial = markings;
        }

        /// <summary>
        /// The floor's whole top face, as two quads in one mesh: the playing area, mapped to
        /// the pack's court so the key and the arc come out at their real size, and the
        /// run-off behind the baseline, mapped to bare boards. One mesh and one material
        /// rather than two objects, because this is a headset's draw call either way.
        /// </summary>
        private static Mesh BuildCourtSurfaceMesh(CourtSettings s)
        {
            float halfWidth = s.CourtWidth * 0.5f;
            float vPerMetre = (PackVMax - PackVMin) / PackCourtLength;

            // The court, laid from the baseline back towards the player.
            float courtNear = s.Baseline;
            float courtFar = s.FloorFrontEdge;
            float vCourtFar = PackVMax - (courtNear - courtFar) * vPerMetre;

            // The run-off, from the baseline out to the floor's back edge.
            float offNear = s.Baseline;
            float offFar = s.FloorBackEdge;
            float vOffFar = PackWoodV + (offFar - offNear) * vPerMetre;

            var mesh = new Mesh { name = "CourtSurface" };
            mesh.vertices = new[]
            {
                new Vector3(-halfWidth, 0f, courtFar),
                new Vector3(halfWidth, 0f, courtFar),
                new Vector3(halfWidth, 0f, courtNear),
                new Vector3(-halfWidth, 0f, courtNear),

                new Vector3(-halfWidth, 0f, offNear),
                new Vector3(halfWidth, 0f, offNear),
                new Vector3(halfWidth, 0f, offFar),
                new Vector3(-halfWidth, 0f, offFar)
            };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.uv = new[]
            {
                new Vector2(PackUMin, vCourtFar),
                new Vector2(PackUMax, vCourtFar),
                new Vector2(PackUMax, PackVMax),
                new Vector2(PackUMin, PackVMax),

                new Vector2(PackUMin, PackWoodV),
                new Vector2(PackUMax, PackWoodV),
                new Vector2(PackUMax, vOffFar),
                new Vector2(PackUMin, vOffFar)
            };
            mesh.triangles = new[] { 0, 3, 2, 0, 2, 1, 4, 7, 6, 4, 6, 5 };
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// A material from the arena pack. Missing is reported rather than substituted
        /// silently, because a court that quietly loses its markings still looks built.
        /// </summary>
        private static Material PackMaterial(string name)
        {
            string path = PackMaterials + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
                Debug.LogWarning("Arena pack material not found at " + path + "; falling back to a plain surface.");
            return material;
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

            // The pack's hoop is one mesh carrying board, rim and stanchion together, so it
            // goes in whole as the visual and everything built below it is collider only.
            // Its measurements are in CourtSettings, which is what keeps the two aligned.
            GameObject visual = BuildArenaHoop(hoop.transform);
            bool dressed = visual != null;

            GameObject backboard = Box("Backboard", hoop.transform,
                new Vector3(0f, s.BackboardBottomHeight + s.BackboardHeight * 0.5f, s.RimCentreToBackboardFace + s.BackboardThickness * 0.5f),
                new Vector3(s.BackboardWidth, s.BackboardHeight, s.BackboardThickness),
                Surface("Backboard", new Color(0.88f, 0.88f, 0.90f)));
            backboard.GetComponent<BoxCollider>().sharedMaterial = board;
            if (dressed)
                Invisible(backboard);

            BasketSensor sensor = BuildRing(hoop.transform, s, ring, dressed);
            BuildPost(hoop.transform, s, board, dressed);
            BuildScoreboard(hoop.transform, s, score, board);

            score.Sensors = new[] { sensor };
        }

        private static BasketSensor BuildRing(Transform hoop, CourtSettings s, PhysicsMaterial surface, bool dressed)
        {
            GameObject ring = Child("Ring", hoop, new Vector3(0f, s.RimCentreHeight, 0f));

            // The generated torus is the rim's stand-in. When the pack's hoop is in, that
            // mesh is already drawn there, and a second one only z-fights with it.
            if (!dressed)
            {
                GameObject visual = Child("Ring Mesh", ring.transform, Vector3.zero);
                visual.AddComponent<MeshFilter>().sharedMesh = SaveRingMesh(s);
                visual.AddComponent<MeshRenderer>().sharedMaterial = Surface("Ring", new Color(0.86f, 0.31f, 0.09f));
            }
            else
            {
                BuildNet(ring.transform, s);
            }

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
        private static void BuildScoreboard(Transform hoop, CourtSettings s, ScoreKeeper score, PhysicsMaterial surface)
        {
            float width = ScoreDigits * DigitWidth + (ScoreDigits - 1) * DigitGap + PanelMargin * 2f;
            float height = DigitHeight + PanelMargin * 2f;

            GameObject board = Child("Scoreboard", hoop,
                new Vector3(0f, s.BackboardBottomHeight + s.BackboardHeight + height * 0.5f,
                    s.RimCentreToBackboardFace + s.BackboardThickness * 0.5f));

            // The panel is solid. It sits directly above the board, in the path of a shot
            // that comes up short over the top, and a ball that passed through it read as a
            // hole in the hoop. It shares the backboard's surface because it is one.
            GameObject panel = Box("Panel", board.transform, Vector3.zero, new Vector3(width, height, PanelDepth),
                Surface("Scoreboard", new Color(0.05f, 0.06f, 0.08f)));
            panel.GetComponent<BoxCollider>().sharedMaterial = surface;

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

        /// <summary>
        /// What holds the board up. When the pack's hoop is dressing the scene these are its
        /// stanchion's own measurements, so a ball that hits the pole stops at the pole; on
        /// the bare court they fall back to a post standing clear of any shot.
        /// </summary>
        private static void BuildPost(Transform hoop, CourtSettings s, PhysicsMaterial surface, bool dressed)
        {
            const float Clearance = 0.6f;
            const float Thickness = 0.16f;

            GameObject post = Child("Post", hoop, Vector3.zero);
            Material material = Surface("Post", new Color(0.20f, 0.21f, 0.24f));

            float behindBoard = s.RimCentreToBackboardFace + s.BackboardThickness;
            float columnZ = dressed ? ArenaPoleBack : behindBoard + Clearance + Thickness * 0.5f;
            float thickness = dressed ? ArenaPoleWidth : Thickness;
            float top = dressed ? ArenaPoleTop : s.BackboardBottomHeight;

            GameObject column = Box("Column", post.transform,
                new Vector3(0f, top * 0.5f, columnZ),
                new Vector3(thickness, top, thickness), material);
            column.GetComponent<BoxCollider>().sharedMaterial = surface;

            float armNear = dressed ? ArenaArmNear : behindBoard;
            float armFar = columnZ + thickness * 0.5f;
            GameObject arm = Box("Arm", post.transform,
                new Vector3(0f, top - Thickness * 0.5f, (armNear + armFar) * 0.5f),
                new Vector3(Thickness, Thickness, armFar - armNear), material);
            arm.GetComponent<BoxCollider>().sharedMaterial = surface;

            if (dressed)
            {
                Invisible(column);
                Invisible(arm);
            }
        }

        /// <summary>
        /// The pack's hoop, placed by its rim rather than by its origin: the mesh is turned to
        /// face the player and then shifted so the point its rim is modelled around lands on
        /// the ring this court built. Returns null when the pack is not present, which leaves
        /// the court to draw its own plain hoop.
        /// </summary>
        private static GameObject BuildArenaHoop(Transform hoop)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaHoopPrefab);
            if (prefab == null)
            {
                Debug.LogWarning("Arena hoop not found at " + ArenaHoopPrefab + "; building a plain hoop instead.");
                return null;
            }

            var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, hoop);
            Undo.RegisterCreatedObjectUndo(visual, "Rebuild Court");
            visual.name = "Hoop Mesh";
            visual.transform.localRotation = ArenaFacing;
            visual.transform.localPosition = -(ArenaFacing * ArenaRimOffset) + new Vector3(0f, ArenaRimOffset.y, 0f);
            GameObjectUtility.SetStaticEditorFlags(visual, (StaticEditorFlags)~0);
            return visual;
        }

        /// <summary>
        /// The net, hung from the ring. It is scaled by the rim it was modelled around, so a
        /// court built to other dimensions still gets a net that meets its own rim.
        /// </summary>
        private static void BuildNet(Transform ring, CourtSettings s)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaNetPrefab);
            if (prefab == null)
                return;

            var net = (GameObject)PrefabUtility.InstantiatePrefab(prefab, ring);
            Undo.RegisterCreatedObjectUndo(net, "Rebuild Court");
            net.name = "Net";
            float scale = s.RimCentrelineRadius / ArenaNetRadius;
            net.transform.localScale = Vector3.one * scale;
            net.transform.localPosition = new Vector3(0f, -ArenaNetTop * scale, 0f);
            GameObjectUtility.SetStaticEditorFlags(net, (StaticEditorFlags)~0);
        }

        /// <summary>Takes the drawing off something, leaving whatever collider it carries.</summary>
        private static void Invisible(GameObject go)
        {
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
                Object.DestroyImmediate(renderer);

            var filter = go.GetComponent<MeshFilter>();
            if (filter != null)
                Object.DestroyImmediate(filter);
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
            return SaveMesh(BuildRingMesh(s.RimCentrelineRadius, s.RimTubeRadius, s.RimSegments, RingMeshTubeSides), RingMeshPath);
        }

        /// <summary>
        /// Writes a generated mesh to an asset, so the scene keeps a real reference rather
        /// than one that only lives until the next reload. An existing asset is overwritten
        /// in place instead of replaced, so references to it survive the rebuild.
        /// </summary>
        private static Mesh SaveMesh(Mesh mesh, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }

            existing.Clear();
            existing.vertices = mesh.vertices;
            existing.normals = mesh.normals;
            existing.tangents = mesh.tangents;
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
