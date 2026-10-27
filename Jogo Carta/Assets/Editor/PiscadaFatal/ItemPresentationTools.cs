using System.Collections.Generic;
using System.IO;
using ProjetoCarta.Interaction;
using ProjetoCarta.Inventory;
using UnityEditor;
using UnityEngine;

namespace ProjetoCarta.EditorTools
{
    /// <summary>
    /// Apresentacao dos itens da Piscada Fatal a partir dos modelos reais da cena:
    /// - gera o icone de cada item (render 3/4 com fundo transparente) para a roda do inventario e os avisos;
    /// - calcula a pose na mao (escala uniforme pelo tamanho desejado, rotacao e centralizacao no ItemMount).
    /// Rode com a cena 1 aberta (fora do Play Mode).
    /// </summary>
    public static class ItemPresentationTools
    {
        public const string ItemsFolder = "Assets/PiscadaFatal/Items";
        public const string IconsFolder = ItemsFolder + "/Icones";
        private const int IconSize = 256;
        private const int RenderLayer = 30;

        /// <summary>Tamanho (maior lado, em metros) de cada item na mao.</summary>
        public static readonly Dictionary<string, float> HandSize = new()
        {
            { "abridor_cartas", 0.2f },
            { "balde", 0.26f },
            { "caneta_tinteiro", 0.17f },
            { "chave_amarela", 0.13f },
            { "chave_camara", 0.15f },
            { "chave_prateada", 0.12f },
            { "envelope_lacrado", 0.2f },
            { "ficha_telefone", 0.065f },
            { "ima", 0.11f },
            { "maca", 0.09f },
            { "olho_vidro", 0.06f },
            { "pasta_arquivos", 0.24f },
            { "pe_cabra", 0.42f },
            { "relogio_bolso", 0.09f },
            { "manivela_poco", 0.26f }
        };

        [MenuItem("Projeto Carta/Piscada Fatal/Gerar icones dos itens")]
        public static void GenerateAllIcons()
        {
            if (!AssetDatabase.IsValidFolder(IconsFolder))
                AssetDatabase.CreateFolder(ItemsFolder, "Icones");

            foreach (KeyValuePair<InventoryItemDefinition, GameObject> pair in FindItemModels())
                GenerateIcon(pair.Key, pair.Value);

            AssetDatabase.SaveAssets();
        }

        /// <summary>Encontra, na cena aberta, o objeto de mundo de cada item (pelo InventoryPickup).</summary>
        public static Dictionary<InventoryItemDefinition, GameObject> FindItemModels()
        {
            Dictionary<InventoryItemDefinition, GameObject> result = new();
            foreach (InventoryPickup pickup in Object.FindObjectsByType<InventoryPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (pickup.Item == null || result.ContainsKey(pickup.Item))
                    continue;

                SerializedObject so = new(pickup);
                GameObject model = so.FindProperty("itemObject").objectReferenceValue as GameObject;
                result[pickup.Item] = model != null ? model : pickup.gameObject;
            }

            return result;
        }

        public static void GenerateIcon(InventoryItemDefinition item, GameObject source)
        {
            GameObject clone = CloneVisual(source);
            Vector3 viewDirection = new Vector3(-0.55f, 0.6f, -1f).normalized;
            // O olho de vidro tem a iris no -Y do modelo: vira a iris para a camera.
            if (item.Id == "olho_vidro")
                clone.transform.rotation = Quaternion.FromToRotation(Vector3.down, viewDirection);
            Bounds bounds = WorldBounds(clone);

            GameObject cameraGo = new("IconCamera") { hideFlags = HideFlags.HideAndDontSave };
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.cullingMask = 1 << RenderLayer;
            camera.fieldOfView = 22f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 50f;
            camera.allowHDR = false;
            camera.allowMSAA = true;

            float radius = bounds.extents.magnitude;
            float distance = radius / Mathf.Sin(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.02f;
            camera.transform.position = bounds.center + viewDirection * distance;
            camera.transform.LookAt(bounds.center);

            List<Light> lights = new();
            lights.Add(MakeLight(Quaternion.Euler(40f, -30f, 0f), new Color(1f, 0.93f, 0.82f), 1.3f));
            lights.Add(MakeLight(Quaternion.Euler(-20f, 150f, 0f), new Color(0.55f, 0.65f, 0.8f), 0.6f));

            RenderTexture target = new(IconSize, IconSize, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            camera.targetTexture = target;
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            Texture2D texture = new(IconSize, IconSize, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, IconSize, IconSize), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;

            string path = IconsFolder + "/" + item.Id + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG());

            Object.DestroyImmediate(texture);
            camera.targetTexture = null;
            target.Release();
            Object.DestroyImmediate(target);
            foreach (Light light in lights)
                Object.DestroyImmediate(light.gameObject);
            Object.DestroyImmediate(cameraGo);
            Object.DestroyImmediate(clone);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();

            SerializedObject so = new(item);
            so.FindProperty("icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
        }

        /// <summary>
        /// Calcula a pose na mao: escala uniforme para o tamanho de <see cref="HandSize"/>, maior eixo do modelo
        /// apontando para frente e um pouco para a esquerda/cima, centro do modelo no ponto de montagem.
        /// </summary>
        public static void ComputeHandPose(InventoryItemDefinition item, GameObject source, Vector3 euler)
        {
            if (!HandSize.TryGetValue(item.Id, out float size))
                return;

            Bounds local = LocalBounds(source);
            // Proporcoes do objeto na cena (alguns modelos dependem de escala nao uniforme, ex.: a ficha).
            Vector3 lossy = source.transform.lossyScale;
            Vector3 ratio = new Vector3(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y), Mathf.Abs(lossy.z));
            Vector3 dims = Vector3.Scale(local.size, ratio);
            float largest = Mathf.Max(dims.x, Mathf.Max(dims.y, dims.z));
            if (largest <= 0.000001f)
                return;

            Vector3 scaleVector = ratio * (size / largest);
            Quaternion rotation = Quaternion.Euler(euler);
            Vector3 position = -(rotation * Vector3.Scale(local.center, scaleVector));

            SerializedObject so = new(item);
            so.FindProperty("equippedLocalPosition").vector3Value = position;
            so.FindProperty("equippedLocalEulerAngles").vector3Value = euler;
            so.FindProperty("equippedLocalScale").vector3Value = scaleVector;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
        }


        private enum HoldStyle { Long, Flat, Upright }

        private static readonly Dictionary<string, HoldStyle> HoldStyles = new()
        {
            { "abridor_cartas", HoldStyle.Long },
            { "caneta_tinteiro", HoldStyle.Long },
            { "pe_cabra", HoldStyle.Long },
            { "manivela_poco", HoldStyle.Long },
            { "chave_amarela", HoldStyle.Long },
            { "chave_camara", HoldStyle.Long },
            { "chave_prateada", HoldStyle.Long },
            { "envelope_lacrado", HoldStyle.Flat },
            { "pasta_arquivos", HoldStyle.Flat },
            { "ficha_telefone", HoldStyle.Flat },
            { "relogio_bolso", HoldStyle.Flat },
            { "maca", HoldStyle.Upright },
            { "olho_vidro", HoldStyle.Upright },
            { "ima", HoldStyle.Upright },
            { "balde", HoldStyle.Upright }
        };

        [MenuItem("Projeto Carta/Piscada Fatal/Calcular pose dos itens na mao")]
        public static void ComputeAllHandPoses()
        {
            foreach (KeyValuePair<InventoryItemDefinition, GameObject> pair in FindItemModels())
            {
                if (!HoldStyles.TryGetValue(pair.Key.Id, out HoldStyle style))
                    continue;

                Quaternion rotation = HandRotation(pair.Key.Id, pair.Value, style);
                ComputeHandPose(pair.Key, pair.Value, rotation.eulerAngles);
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Rotacao no ItemMount (+Z frente da camera, +Y cima, +X direita):
        /// longos apontam para frente com o lado grosso (cabo, argola) perto da mao;
        /// planos mostram a face para a camera; os demais ficam em pe, levemente virados.
        /// </summary>
        private static Quaternion HandRotation(string id, GameObject model, HoldStyle style)
        {
            List<Vector3> points = RootSpaceVertices(model);
            Bounds bounds = LocalBounds(model);
            Vector3 size = bounds.size;
            size = Vector3.Scale(size, Abs(model.transform.lossyScale));
            Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };
            int longest = 0, thinnest = 0;
            for (int i = 1; i < 3; i++)
            {
                if (size[i] > size[longest]) longest = i;
                if (size[i] < size[thinnest]) thinnest = i;
            }

            if (thinnest == longest)
                thinnest = (longest + 1) % 3;

            Vector3 longAxis = axes[longest];
            Vector3 thinAxis = axes[thinnest];

            switch (style)
            {
                case HoldStyle.Long:
                {
                    // O lado mais "gordo" (cabo, argola da chave, gancho) fica perto da mao.
                    if (ThickEndIsPositive(points, bounds, longest))
                        longAxis = -longAxis;

                    Vector3 forward = new Vector3(-0.32f, 0.38f, 0.87f).normalized;
                    Vector3 faceUp = new Vector3(0.1f, 0.6f, -0.8f).normalized;
                    Quaternion pose = Quaternion.LookRotation(forward, faceUp) * Quaternion.Inverse(Quaternion.LookRotation(longAxis, thinAxis));
                    // Chaves: gira no proprio eixo para a face (argola e dentes) ficar de frente para a camera.
                    if (id.StartsWith("chave"))
                        pose = Quaternion.AngleAxis(90f, forward) * pose;
                    return pose;
                }
                case HoldStyle.Flat:
                {
                    // Face larga para a camera, maior lado na horizontal com leve inclinacao.
                    Vector3 toCamera = new Vector3(-0.22f, 0.3f, -0.93f).normalized;
                    Vector3 across = new Vector3(0.97f, 0.2f, 0.1f).normalized;
                    return Quaternion.LookRotation(toCamera, Vector3.Cross(toCamera, across)) *
                           Quaternion.Inverse(Quaternion.LookRotation(thinAxis, Vector3.Cross(thinAxis, longAxis)));
                }
                default:
                {
                    if (id == "olho_vidro")
                        return Quaternion.FromToRotation(Vector3.down, new Vector3(-0.3f, 0.15f, -0.94f).normalized);

                    return Quaternion.Euler(0f, -28f, 0f);
                }
            }
        }

        /// <summary>
        /// Rotacao de exposicao num encaixe/ancora cujo +Z aponta para quem olha:
        /// longos e planos ficam com a face para o observador; os demais em pe (olho com a iris para fora).
        /// </summary>
        public static Quaternion DisplayRotation(string id, GameObject model)
        {
            if (!HoldStyles.TryGetValue(id, out HoldStyle style))
                return Quaternion.identity;

            Bounds bounds = LocalBounds(model);
            Vector3 size = bounds.size;
            // Mede com a escala real do objeto (a ficha, por exemplo, e achatada pela escala).
            size = Vector3.Scale(size, Abs(model.transform.lossyScale));
            Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };
            int longest = 0, thinnest = 0;
            for (int i = 1; i < 3; i++)
            {
                if (size[i] > size[longest]) longest = i;
                if (size[i] < size[thinnest]) thinnest = i;
            }

            if (thinnest == longest)
                thinnest = (longest + 1) % 3;

            Vector3 longAxis = axes[longest];
            Vector3 thinAxis = axes[thinnest];
            Vector3 towardViewer = Vector3.forward;

            switch (style)
            {
                case HoldStyle.Long:
                {
                    if (ThickEndIsPositive(RootSpaceVertices(model), bounds, longest))
                        longAxis = -longAxis;
                    Vector3 across = new Vector3(0.94f, 0.34f, 0f).normalized;
                    return Quaternion.LookRotation(towardViewer, across) * Quaternion.Inverse(Quaternion.LookRotation(thinAxis, longAxis));
                }
                case HoldStyle.Flat:
                    return Quaternion.LookRotation(towardViewer, Vector3.up) *
                           Quaternion.Inverse(Quaternion.LookRotation(thinAxis, Vector3.Cross(longAxis, thinAxis)));
                default:
                    return id == "olho_vidro"
                        ? Quaternion.FromToRotation(Vector3.down, towardViewer)
                        : Quaternion.Euler(0f, 180f, 0f);
            }
        }

        [MenuItem("Projeto Carta/Piscada Fatal/Ajustar objetos expostos no painel do ritual")]
        public static void ApplyRitualPanelPoses()
        {
            Dictionary<InventoryItemDefinition, GameObject> models = FindItemModels();
            foreach (InventoryItemPlacement slot in Object.FindObjectsByType<InventoryItemPlacement>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (slot.RequiredItem == null || !models.TryGetValue(slot.RequiredItem, out GameObject model))
                    continue;

                SerializedObject so = new(slot);
                so.FindProperty("targetLocalEulerAngles").vector3Value = DisplayRotation(slot.RequiredItem.Id, model).eulerAngles;
                so.FindProperty("targetLocalPosition").vector3Value = Vector3.zero;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(slot);
            }
        }

        private static Vector3 Abs(Vector3 v)
        {
            return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        }

        private static bool ThickEndIsPositive(List<Vector3> points, Bounds bounds, int axis)
        {
            float min = bounds.min[axis], max = bounds.max[axis];
            float span = Mathf.Max(0.0001f, max - min);
            float lowSpread = 0f, highSpread = 0f;
            int other1 = (axis + 1) % 3, other2 = (axis + 2) % 3;
            foreach (Vector3 p in points)
            {
                float t = (p[axis] - min) / span;
                float spread = Mathf.Abs(p[other1] - bounds.center[other1]) + Mathf.Abs(p[other2] - bounds.center[other2]);
                if (t < 0.3f) lowSpread = Mathf.Max(lowSpread, spread);
                else if (t > 0.7f) highSpread = Mathf.Max(highSpread, spread);
            }

            return highSpread > lowSpread;
        }

        private static List<Vector3> RootSpaceVertices(GameObject root)
        {
            List<Vector3> points = new();
            Matrix4x4 toRoot = root.transform.worldToLocalMatrix;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || !filter.sharedMesh.isReadable)
                    continue;

                Matrix4x4 matrix = toRoot * filter.transform.localToWorldMatrix;
                foreach (Vector3 vertex in filter.sharedMesh.vertices)
                    points.Add(matrix.MultiplyPoint3x4(vertex));
            }

            return points;
        }

        // ------------------------------------------------------------ helpers

        private static GameObject CloneVisual(GameObject source)
        {
            GameObject clone = Object.Instantiate(source);
            clone.hideFlags = HideFlags.HideAndDontSave;
            clone.SetActive(true);
            clone.transform.SetPositionAndRotation(new Vector3(0f, -500f, 0f), Quaternion.identity);
            clone.transform.localScale = source.transform.lossyScale;

            foreach (Component component in clone.GetComponentsInChildren<Component>(true))
            {
                if (component is Transform || component is MeshFilter || component is Renderer)
                    continue;
                Object.DestroyImmediate(component);
            }

            foreach (Transform t in clone.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = RenderLayer;
                t.gameObject.SetActive(true);
            }

            return clone;
        }

        private static Light MakeLight(Quaternion rotation, Color color, float intensity)
        {
            GameObject go = new("IconLight") { hideFlags = HideFlags.HideAndDontSave };
            go.transform.rotation = rotation;
            Light light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.cullingMask = 1 << RenderLayer;
            light.shadows = LightShadows.None;
            return light;
        }

        private static Bounds WorldBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(root.transform.position, Vector3.one * 0.1f);
            foreach (Renderer renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        /// <summary>Limites no espaco local do objeto raiz (independe de onde ele esta).</summary>
        public static Bounds LocalBounds(GameObject root)
        {
            Matrix4x4 toRoot = root.transform.worldToLocalMatrix;
            bool has = false;
            Bounds bounds = default;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                    continue;

                Matrix4x4 matrix = toRoot * filter.transform.localToWorldMatrix;
                Bounds mesh = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mesh.center + Vector3.Scale(mesh.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 point = matrix.MultiplyPoint3x4(corner);
                    if (!has)
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        has = true;
                    }
                    else
                    {
                        bounds.Encapsulate(point);
                    }
                }
            }

            return bounds;
        }
    }
}
