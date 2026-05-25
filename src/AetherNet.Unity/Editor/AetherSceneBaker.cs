using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using AetherNet.Server;

namespace AetherNet.Editor
{
    /// <summary>
    /// Scans the scene for AetherRigidbody + collider components and exports a
    /// MapData JSON file that the headless server can load without Unity installed.
    /// </summary>
    public static class AetherSceneBaker
    {
        [MenuItem("AetherNet/Bake Scene to JSON")]
        public static void BakeScene()
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            string savePath  = EditorUtility.SaveFilePanel(
                "Save Baked Map", "Assets/StreamingAssets", sceneName, "json");
            if (string.IsNullOrEmpty(savePath)) return;

            MapData map  = BuildMapData(sceneName);
            string  json = System.Text.Json.JsonSerializer.Serialize(
                map, MapSerializerContext.Default.MapData,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(savePath, json);

            if (savePath.StartsWith(Application.dataPath))
                AssetDatabase.ImportAsset("Assets" + savePath[Application.dataPath.Length..]);

            Debug.Log($"[AetherNet] Baked {map.Entities.Length} entities to: {savePath}");
            EditorUtility.DisplayDialog("AetherNet Baker",
                $"Baked {map.Entities.Length} entities.\n\nSaved to:\n{savePath}", "OK");
        }

        private static MapData BuildMapData(string sceneName)
        {
#if UNITY_2022_1_OR_NEWER
            var rigidbodies = Object.FindObjectsByType<AetherRigidbody>(FindObjectsSortMode.None);
#else
            var rigidbodies = Object.FindObjectsOfType<AetherRigidbody>();
#endif
            var entities = new List<BakedEntityDef>(rigidbodies.Length);
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                AetherRigidbody rb = rigidbodies[i];
                Transform       tf = rb.transform;
                entities.Add(new BakedEntityDef
                {
                    EntityId       = i,
                    BodyType       = rb.BodyType,
                    PositionX      = tf.position.x / SimulationConstants.PixelsPerMeter,
                    PositionY      = tf.position.y / SimulationConstants.PixelsPerMeter,
                    Angle          = MathExtensions.ToSimAngle(tf.eulerAngles.z),
                    LinearDamping  = rb.LinearDamping,
                    AngularDamping = rb.AngularDamping,
                    GravityScale   = rb.GravityScale,
                    FixedRotation  = rb.FixedRotation,
                    Constraints    = rb.Constraints,
                    Fixtures       = BuildFixtures(rb),
                });
            }
            return new MapData { MapName = sceneName, Entities = entities.ToArray() };
        }

        private static BakedFixtureDef[] BuildFixtures(AetherRigidbody rb)
        {
            var result = new List<BakedFixtureDef>();

            foreach (var box in rb.GetComponents<AetherBoxCollider>())
            {
                var so = new SerializedObject(box);
                result.Add(new BakedFixtureDef
                {
                    Shape     = BakedFixtureShape.Box,
                    Width     = so.FindProperty("_size").vector2Value.x  / SimulationConstants.PixelsPerMeter,
                    Height    = so.FindProperty("_size").vector2Value.y  / SimulationConstants.PixelsPerMeter,
                    OffsetX   = so.FindProperty("_offset").vector2Value.x / SimulationConstants.PixelsPerMeter,
                    OffsetY   = so.FindProperty("_offset").vector2Value.y / SimulationConstants.PixelsPerMeter,
                    IsSensor  = so.FindProperty("_isTrigger").boolValue,
                    Layer     = so.FindProperty("_layer").intValue,
                    Density   = ReadMaterialField(so, m => m.Density,     1f),
                    Friction  = ReadMaterialField(so, m => m.Friction,    0.2f),
                    Restitution = ReadMaterialField(so, m => m.Restitution, 0f),
                });
            }

            foreach (var circle in rb.GetComponents<AetherCircleCollider>())
            {
                var so = new SerializedObject(circle);
                result.Add(new BakedFixtureDef
                {
                    Shape     = BakedFixtureShape.Circle,
                    Radius    = so.FindProperty("_radius").floatValue / SimulationConstants.PixelsPerMeter,
                    OffsetX   = so.FindProperty("_offset").vector2Value.x / SimulationConstants.PixelsPerMeter,
                    OffsetY   = so.FindProperty("_offset").vector2Value.y / SimulationConstants.PixelsPerMeter,
                    IsSensor  = so.FindProperty("_isTrigger").boolValue,
                    Layer     = so.FindProperty("_layer").intValue,
                    Density   = ReadMaterialField(so, m => m.Density,     1f),
                    Friction  = ReadMaterialField(so, m => m.Friction,    0.2f),
                    Restitution = ReadMaterialField(so, m => m.Restitution, 0f),
                });
            }

            foreach (var poly in rb.GetComponents<AetherPolygonCollider>())
            {
                var so    = new SerializedObject(poly);
                var verts = so.FindProperty("_vertices");
                var xs    = new float[verts.arraySize];
                var ys    = new float[verts.arraySize];
                for (int v = 0; v < verts.arraySize; v++)
                {
                    Vector2 pt = verts.GetArrayElementAtIndex(v).vector2Value;
                    xs[v] = pt.x / SimulationConstants.PixelsPerMeter;
                    ys[v] = pt.y / SimulationConstants.PixelsPerMeter;
                }
                result.Add(new BakedFixtureDef
                {
                    Shape      = BakedFixtureShape.Polygon,
                    VerticesX  = xs,
                    VerticesY  = ys,
                    IsSensor   = so.FindProperty("_isTrigger").boolValue,
                    Layer      = so.FindProperty("_layer").intValue,
                    Density    = ReadMaterialField(so, m => m.Density,     1f),
                    Friction   = ReadMaterialField(so, m => m.Friction,    0.2f),
                    Restitution = ReadMaterialField(so, m => m.Restitution, 0f),
                });
            }

            return result.ToArray();
        }

        private static float ReadMaterialField(
            SerializedObject so,
            System.Func<AetherPhysicsMaterial, float> getter,
            float fallback)
        {
            var mat = so.FindProperty("_material").objectReferenceValue as AetherPhysicsMaterial;
            return mat != null ? getter(mat) : fallback;
        }
    }
}
