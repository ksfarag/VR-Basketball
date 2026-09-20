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
    /// </summary>
    public static class CourtBuilder
    {
        private const string RootName = "Court";
        private const string RingMeshPath = "Assets/Meshes/Ring.mesh";
        private const int RingMeshTubeSides = 8;

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

            BuildFloor(root, settings, courtSurface);
            BuildBounds(root, settings, courtSurface);
            BuildHoop(root, settings, boardSurface, ringSurface);

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

        private static void BuildHoop(Transform root, CourtSettings s, PhysicsMaterial board, PhysicsMaterial ring)
        {
            GameObject hoop = Child("Hoop", root, Vector3.zero);

            GameObject backboard = Box("Backboard", hoop.transform,
                new Vector3(0f, s.BackboardBottomHeight + s.BackboardHeight * 0.5f, s.RimCentreToBackboardFace + s.BackboardThickness * 0.5f),
                new Vector3(s.BackboardWidth, s.BackboardHeight, s.BackboardThickness),
                Surface("Backboard", new Color(0.88f, 0.88f, 0.90f)));
            backboard.GetComponent<BoxCollider>().sharedMaterial = board;

            BuildRing(hoop.transform, s, ring);
            BuildPost(hoop.transform, s, board);
        }

        private static void BuildRing(Transform hoop, CourtSettings s, PhysicsMaterial surface)
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

        private static GameObject Box(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material)
        {
            GameObject go = Child(name, parent, localPosition);
            go.AddComponent<MeshFilter>().sharedMesh = PrimitiveMesh(PrimitiveType.Cube);
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            go.AddComponent<BoxCollider>();
            go.transform.localScale = size;
            return go;
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
