using System;
using System.Collections.Generic;
using ProjetoCarta.Interaction;
using ProjetoCarta.Inventory;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

public static class PiscadaFatalSceneSetupEditor
{
public static object Run()
{
const string RootName = "PiscadaFatal_Cenarios";
const string ContentFolder = "Assets/PiscadaFatal";
const string ItemFolder = ContentFolder + "/Items";
const string MaterialFolder = ContentFolder + "/Materials";

GameObject previousRoot = GameObject.Find("/" + RootName);
if (previousRoot != null)
    throw new InvalidOperationException("A raiz '" + RootName + "' ja existe. A montagem foi interrompida para evitar duplicacao.");

void EnsureFolder(string parent, string child)
{
    string path = parent + "/" + child;
    if (!AssetDatabase.IsValidFolder(path))
        AssetDatabase.CreateFolder(parent, child);
}

EnsureFolder("Assets", "PiscadaFatal");
EnsureFolder(ContentFolder, "Items");
EnsureFolder(ContentFolder, "Materials");

Material GetMaterial(string name, Color color, float metallic = 0f, float smoothness = 0.2f)
{
    string path = MaterialFolder + "/" + name + ".mat";
    Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
    if (material == null)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        material = new Material(shader) { name = name, color = color };
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        AssetDatabase.CreateAsset(material, path);
    }
    return material;
}

InventoryItemDefinition GetItem(string id, string displayName, Vector3 equippedScale)
{
    string path = ItemFolder + "/" + id + ".asset";
    InventoryItemDefinition item = AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(path);
    if (item == null)
    {
        item = ScriptableObject.CreateInstance<InventoryItemDefinition>();
        item.name = displayName;
        AssetDatabase.CreateAsset(item, path);
    }

    SerializedObject serialized = new SerializedObject(item);
    serialized.FindProperty("id").stringValue = id;
    serialized.FindProperty("displayName").stringValue = displayName;
    serialized.FindProperty("description").stringValue = "Item de puzzle de Piscada Fatal.";
    serialized.FindProperty("equippedLocalPosition").vector3Value = new Vector3(0f, 0f, 0.25f);
    serialized.FindProperty("equippedLocalEulerAngles").vector3Value = Vector3.zero;
    serialized.FindProperty("equippedLocalScale").vector3Value = equippedScale;
    serialized.ApplyModifiedPropertiesWithoutUndo();
    EditorUtility.SetDirty(item);
    return item;
}

GameObject Empty(string name, Transform parent, Vector3 localPosition)
{
    GameObject result = new GameObject(name);
    result.transform.SetParent(parent, false);
    result.transform.localPosition = localPosition;
    return result;
}

GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
{
    GameObject result = GameObject.CreatePrimitive(type);
    result.name = name;
    result.transform.SetParent(parent, false);
    result.transform.localPosition = localPosition;
    result.transform.localScale = localScale;
    Renderer renderer = result.GetComponent<Renderer>();
    if (renderer != null)
        renderer.sharedMaterial = material;
    return result;
}

void SetPrompt(RaycastInteractable interactable, string prompt)
{
    SerializedObject serialized = new SerializedObject(interactable);
    serialized.FindProperty("interactionPrompt").stringValue = prompt;
    serialized.ApplyModifiedPropertiesWithoutUndo();
}

GameObject Pickup(string name, InventoryItemDefinition item, Transform parent, Vector3 localPosition,
    PrimitiveType type, Vector3 scale, Material material, bool active = true)
{
    GameObject result = Primitive(name, type, parent, localPosition, scale, material);
    InventoryPickup pickup = result.AddComponent<InventoryPickup>();
    SerializedObject serialized = new SerializedObject(pickup);
    serialized.FindProperty("interactionPrompt").stringValue = "Pegar " + item.DisplayName;
    serialized.FindProperty("item").objectReferenceValue = item;
    serialized.FindProperty("itemObject").objectReferenceValue = result;
    serialized.ApplyModifiedPropertiesWithoutUndo();
    result.SetActive(active);
    return result;
}

InventoryItemPlacement Placement(string name, InventoryItemDefinition requiredItem, Transform parent,
    Vector3 localPosition, Vector3 localScale, Material material, Vector3 placedPosition,
    Vector3 placedEuler, Vector3 placedScale)
{
    GameObject socket = Primitive(name, PrimitiveType.Cube, parent, localPosition, localScale, material);
    InventoryItemPlacement placement = socket.AddComponent<InventoryItemPlacement>();
    SerializedObject serialized = new SerializedObject(placement);
    serialized.FindProperty("interactionPrompt").stringValue = "Colocar " + requiredItem.DisplayName;
    serialized.FindProperty("requiredItem").objectReferenceValue = requiredItem;
    serialized.FindProperty("targetLocalPosition").vector3Value = placedPosition;
    serialized.FindProperty("targetLocalEulerAngles").vector3Value = placedEuler;
    serialized.FindProperty("targetLocalScale").vector3Value = placedScale;
    serialized.FindProperty("interpolationDuration").floatValue = 0.55f;
    serialized.ApplyModifiedPropertiesWithoutUndo();
    return placement;
}

void AddRoomShell(Transform parent, Material floorMaterial, Material wallMaterial)
{
    GameObject shell = Empty("Sala_12x12", parent, Vector3.zero);
    Primitive("Piso", PrimitiveType.Cube, shell.transform, new Vector3(0f, -0.1f, 0f), new Vector3(12f, 0.2f, 12f), floorMaterial);
    Primitive("Parede_Fundo", PrimitiveType.Cube, shell.transform, new Vector3(0f, 2.5f, 6f), new Vector3(12f, 5f, 0.2f), wallMaterial);
    Primitive("Parede_Esquerda", PrimitiveType.Cube, shell.transform, new Vector3(-6f, 2.5f, 0f), new Vector3(0.2f, 5f, 12f), wallMaterial);
    Primitive("Parede_Direita", PrimitiveType.Cube, shell.transform, new Vector3(6f, 2.5f, 0f), new Vector3(0.2f, 5f, 12f), wallMaterial);
}

Transform AddFixedPoints(Transform parent)
{
    GameObject points = Empty("Pontos_Fixos_3x3", parent, Vector3.zero);
    Vector3[] positions =
    {
        new Vector3(-4f, 0f, 4f), new Vector3(0f, 0f, 4f), new Vector3(4f, 0f, 4f),
        new Vector3(-4f, 0f, 0f), new Vector3(0f, 0f, 0f), new Vector3(4f, 0f, 0f),
        new Vector3(-4f, 0f, -4f), new Vector3(0f, 0f, -4f), new Vector3(4f, 0f, -4f)
    };
    for (int i = 0; i < positions.Length; i++)
        Empty("P" + (i + 1), points.transform, positions[i]);
    return points.transform;
}

GameObject Point(Transform points, int number)
{
    return points.Find("P" + number).gameObject;
}

Material wall = GetMaterial("Parede_Cinza", new Color(0.31f, 0.32f, 0.34f));
Material floor = GetMaterial("Piso_Escuro", new Color(0.17f, 0.14f, 0.12f));
Material wood = GetMaterial("Madeira", new Color(0.28f, 0.13f, 0.06f));
Material green = GetMaterial("Arquivo_Verde", new Color(0.15f, 0.32f, 0.19f), 0.15f);
Material metal = GetMaterial("Metal", new Color(0.40f, 0.43f, 0.46f), 0.75f, 0.55f);
Material glass = GetMaterial("Vidro_Escuro", new Color(0.035f, 0.045f, 0.055f), 0.1f, 0.85f);
Material black = GetMaterial("Breu", Color.black, 0f, 0f);
Material red = GetMaterial("Destaque_Vermelho", new Color(0.52f, 0.08f, 0.06f));
Material gold = GetMaterial("Item_Dourado", new Color(0.86f, 0.56f, 0.08f), 0.55f, 0.5f);
Material blue = GetMaterial("Item_Azul", new Color(0.10f, 0.38f, 0.68f), 0.25f, 0.35f);
Material ivory = GetMaterial("Item_Marfim", new Color(0.86f, 0.82f, 0.70f));

Dictionary<string, InventoryItemDefinition> items = new Dictionary<string, InventoryItemDefinition>
{
    ["abridor_cartas"] = GetItem("abridor_cartas", "Abridor de cartas", new Vector3(0.18f, 0.18f, 0.55f)),
    ["chave_prateada"] = GetItem("chave_prateada", "Chave prateada", new Vector3(0.22f, 0.22f, 0.5f)),
    ["ima"] = GetItem("ima", "Ima", new Vector3(0.35f, 0.35f, 0.35f)),
    ["chave_amarela"] = GetItem("chave_amarela", "Chave amarela", new Vector3(0.22f, 0.22f, 0.5f)),
    ["ficha_telefone"] = GetItem("ficha_telefone", "Ficha de telefone", new Vector3(0.35f, 0.12f, 0.35f)),
    ["fio_cobre"] = GetItem("fio_cobre", "Fio de cobre", new Vector3(0.3f, 0.3f, 0.3f)),
    ["gancho"] = GetItem("gancho", "Gancho", new Vector3(0.28f, 0.28f, 0.45f)),
    ["pe_cabra"] = GetItem("pe_cabra", "Pe de cabra", new Vector3(0.18f, 0.18f, 0.65f)),
    ["olho_vidro"] = GetItem("olho_vidro", "Olho de vidro", new Vector3(0.25f, 0.25f, 0.25f)),
    ["caneta_tinteiro"] = GetItem("caneta_tinteiro", "Caneta-tinteiro", new Vector3(0.16f, 0.16f, 0.55f)),
    ["pao"] = GetItem("pao", "Pao", new Vector3(0.35f, 0.25f, 0.45f)),
    ["isca_improvisada"] = GetItem("isca_improvisada", "Isca improvisada", new Vector3(0.3f, 0.3f, 0.45f)),
    ["envelope_breu"] = GetItem("envelope_breu", "Envelope do Breu", new Vector3(0.5f, 0.08f, 0.34f)),
    ["maca"] = GetItem("maca", "Maca", new Vector3(0.32f, 0.32f, 0.32f)),
    ["relogio_bolso"] = GetItem("relogio_bolso", "Relogio de bolso", new Vector3(0.3f, 0.12f, 0.3f)),
    ["chave_camara"] = GetItem("chave_camara", "Chave da camara", new Vector3(0.22f, 0.22f, 0.55f))
};

GameObject master = new GameObject(RootName);
master.transform.position = new Vector3(-2.4f, 0f, -18.8f);

GameObject p1 = Empty("CENA_1_SENTENCA", master.transform, Vector3.zero);
GameObject p2 = Empty("CENA_2_ULTIMA_NOITE", master.transform, Vector3.zero);
GameObject p3 = Empty("CENA_3_PERDAO", master.transform, Vector3.zero);
GameObject p4 = Empty("CENA_4_CADEIRA", master.transform, Vector3.zero);

AddRoomShell(p1.transform, floor, wall);
AddRoomShell(p2.transform, floor, wall);
AddRoomShell(p3.transform, floor, wall);
AddRoomShell(p4.transform, floor, wall);
Transform points1 = AddFixedPoints(p1.transform);
Transform points2 = AddFixedPoints(p2.transform);
Transform points3 = AddFixedPoints(p3.transform);
Transform points4 = AddFixedPoints(p4.transform);

// P1 — A Sentenca
GameObject arquivo = Primitive("P1_Arquivo_Alto", PrimitiveType.Cube, Point(points1, 1).transform, new Vector3(0f, 1.5f, 0f), new Vector3(1.6f, 3f, 1.1f), green);
Primitive("Porta_Superior", PrimitiveType.Cube, arquivo.transform, new Vector3(0f, 0.27f, -0.51f), new Vector3(0.92f, 0.43f, 0.08f), metal);
Primitive("Porta_Inferior", PrimitiveType.Cube, arquivo.transform, new Vector3(0f, -0.27f, -0.51f), new Vector3(0.92f, 0.43f, 0.08f), metal);
GameObject placaP1 = Primitive("P2_Placa_Solta", PrimitiveType.Cube, Point(points1, 2).transform, new Vector3(0f, 2.2f, 1.75f), new Vector3(1.1f, 0.7f, 0.08f), metal);

GameObject mesa = Empty("P5_Mesa_Escritorio", Point(points1, 5).transform, Vector3.zero);
Primitive("Tampo", PrimitiveType.Cube, mesa.transform, new Vector3(0f, 1f, 0f), new Vector3(3.1f, 0.18f, 1.55f), wood);
Primitive("Gaveta", PrimitiveType.Cube, mesa.transform, new Vector3(0.75f, 0.76f, -0.63f), new Vector3(1.2f, 0.42f, 0.25f), wood);
Primitive("Mata_Borrao", PrimitiveType.Cube, mesa.transform, new Vector3(-0.55f, 1.14f, 0f), new Vector3(0.8f, 0.06f, 0.5f), red);
Primitive("Tinteiro", PrimitiveType.Cylinder, mesa.transform, new Vector3(0.95f, 1.18f, 0.2f), new Vector3(0.18f, 0.16f, 0.18f), black);
Pickup("Abridor_de_Cartas", items["abridor_cartas"], mesa.transform, new Vector3(0.5f, 1.18f, -0.1f), PrimitiveType.Cube, new Vector3(0.12f, 0.08f, 0.7f), metal);
Pickup("Fio_de_Cobre", items["fio_cobre"], Point(points1, 7).transform, new Vector3(0f, 0.12f, 0f), PrimitiveType.Cylinder, new Vector3(0.45f, 0.06f, 0.45f), gold);

GameObject envelopeP1 = Primitive("Envelope_P1", PrimitiveType.Cube, mesa.transform, new Vector3(-0.05f, 1.15f, 0.15f), new Vector3(0.75f, 0.05f, 0.48f), ivory);
Pickup("Chave_Prateada", items["chave_prateada"], envelopeP1.transform, new Vector3(0f, 0.2f, 0f), PrimitiveType.Cube, new Vector3(0.12f, 0.06f, 0.55f), metal, false);
Pickup("Ima", items["ima"], arquivo.transform, new Vector3(0f, 0.2f, -0.7f), PrimitiveType.Cylinder, new Vector3(0.3f, 0.12f, 0.3f), red, false);
Pickup("Chave_Amarela", items["chave_amarela"], arquivo.transform, new Vector3(0f, 0f, 0.7f), PrimitiveType.Cube, new Vector3(0.12f, 0.06f, 0.55f), gold, false);
Pickup("Ficha_de_Telefone", items["ficha_telefone"], arquivo.transform, new Vector3(0f, -0.5f, -0.7f), PrimitiveType.Cylinder, new Vector3(0.28f, 0.08f, 0.28f), gold, false);

GameObject painel = Primitive("Painel_do_Ritual", PrimitiveType.Cube, Point(points1, 3).transform, new Vector3(0f, 2.2f, 1.75f), new Vector3(2.6f, 2.1f, 0.12f), black);
Transform sockets = Empty("Encaixes_5_Objetos", painel.transform, Vector3.zero).transform;
Placement("Encaixe_CANETA", items["caneta_tinteiro"], sockets, new Vector3(0f, 0.27f, -0.58f), new Vector3(0.16f, 0.16f, 0.08f), blue, Vector3.zero, Vector3.zero, new Vector3(0.16f, 0.16f, 0.55f));
Placement("Encaixe_MACA", items["maca"], sockets, new Vector3(-0.25f, 0f, -0.58f), new Vector3(0.16f, 0.16f, 0.08f), red, Vector3.zero, Vector3.zero, Vector3.one * 0.3f);
Placement("Encaixe_RELOGIO", items["relogio_bolso"], sockets, new Vector3(0.25f, 0f, -0.58f), new Vector3(0.16f, 0.16f, 0.08f), gold, Vector3.zero, Vector3.zero, Vector3.one * 0.3f);
Placement("Encaixe_FICHA", items["ficha_telefone"], sockets, new Vector3(-0.25f, -0.27f, -0.58f), new Vector3(0.16f, 0.16f, 0.08f), gold, Vector3.zero, Vector3.zero, Vector3.one * 0.3f);
Placement("Encaixe_OLHO", items["olho_vidro"], sockets, new Vector3(0.25f, -0.27f, -0.58f), new Vector3(0.16f, 0.16f, 0.08f), ivory, Vector3.zero, Vector3.zero, Vector3.one * 0.25f);
GameObject bau = Primitive("Bau_Final", PrimitiveType.Cube, painel.transform, new Vector3(0f, -0.9f, -0.8f), new Vector3(0.9f, 0.55f, 0.55f), wood);
Pickup("Chave_da_Camara", items["chave_camara"], bau.transform, new Vector3(0f, 0.7f, 0f), PrimitiveType.Cube, new Vector3(0.12f, 0.06f, 0.65f), gold, false);

// P2 — A Ultima Noite
GameObject tampa = Primitive("P1_Tampa_Metalica", PrimitiveType.Cube, Point(points2, 1).transform, new Vector3(0f, 2.2f, 1.75f), new Vector3(1.2f, 0.8f, 0.1f), metal);
Primitive("Esquema_Estrela_5_Posicoes", PrimitiveType.Cube, tampa.transform, new Vector3(0f, 0f, 0.65f), new Vector3(0.85f, 0.55f, 0.04f), ivory).SetActive(false);
GameObject relogioParede = Primitive("P3_Relogio_23_58", PrimitiveType.Cylinder, Point(points2, 3).transform, new Vector3(0f, 2.3f, 1.75f), new Vector3(0.85f, 0.12f, 0.85f), ivory);
relogioParede.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
Primitive("Ponteiro_23", PrimitiveType.Cube, relogioParede.transform, new Vector3(0f, 0.55f, -0.1f), new Vector3(0.07f, 0.55f, 0.05f), black);
Primitive("Ponteiro_58", PrimitiveType.Cube, relogioParede.transform, new Vector3(0.4f, 0.05f, -0.12f), new Vector3(0.5f, 0.05f, 0.05f), black);
GameObject banco = Empty("P5_Banco", Point(points2, 5).transform, Vector3.zero);
Primitive("Assento", PrimitiveType.Cube, banco.transform, new Vector3(0f, 0.65f, 0f), new Vector3(2.3f, 0.18f, 0.8f), wood);
Primitive("Pe_E", PrimitiveType.Cube, banco.transform, new Vector3(-0.8f, 0.3f, 0f), new Vector3(0.18f, 0.65f, 0.55f), metal);
Primitive("Pe_D", PrimitiveType.Cube, banco.transform, new Vector3(0.8f, 0.3f, 0f), new Vector3(0.18f, 0.65f, 0.55f), metal);
Primitive("P6_Vidro_de_Observacao", PrimitiveType.Cube, Point(points2, 6).transform, new Vector3(1.8f, 2.5f, 0f), new Vector3(0.12f, 3.2f, 3.6f), glass);

// P3 — O Perdao
Primitive("P1_Armario_Alto", PrimitiveType.Cube, Point(points3, 1).transform, new Vector3(0f, 1.5f, 0f), new Vector3(1.6f, 3f, 1.1f), wood);
GameObject gabinete = Primitive("P3_Gabinete_de_Vidro", PrimitiveType.Cube, Point(points3, 3).transform, new Vector3(0f, 2f, 1.75f), new Vector3(1.4f, 1.2f, 0.45f), glass);
Pickup("Caneta_Tinteiro", items["caneta_tinteiro"], gabinete.transform, new Vector3(0f, 0f, -0.7f), PrimitiveType.Cube, new Vector3(0.1f, 0.1f, 0.65f), blue, false);
GameObject armarioBaixo = Primitive("P5_Armario_Baixo", PrimitiveType.Cube, Point(points3, 5).transform, new Vector3(0f, 0.65f, 0f), new Vector3(2f, 1.3f, 1.2f), wood);
Pickup("Gancho", items["gancho"], armarioBaixo.transform, new Vector3(-0.3f, 0.75f, -0.7f), PrimitiveType.Cylinder, new Vector3(0.22f, 0.08f, 0.22f), metal);
Pickup("Pe_de_Cabra", items["pe_cabra"], armarioBaixo.transform, new Vector3(0.35f, 0.75f, -0.7f), PrimitiveType.Cube, new Vector3(0.1f, 0.1f, 0.9f), red);
GameObject tabua = Primitive("P7_Tabua_Solta", PrimitiveType.Cube, Point(points3, 7).transform, new Vector3(0f, 0.04f, 0f), new Vector3(1.5f, 0.08f, 0.55f), wood);
Pickup("Olho_de_Vidro", items["olho_vidro"], tabua.transform, new Vector3(0f, -0.8f, 0f), PrimitiveType.Sphere, Vector3.one * 0.28f, ivory, false);

// P4 — A Cadeira
GameObject guincho = Empty("P3_Guincho", Point(points4, 3).transform, Vector3.zero);
Primitive("Tripé_E", PrimitiveType.Cylinder, guincho.transform, new Vector3(-0.55f, 1.1f, 0f), new Vector3(0.1f, 1.3f, 0.1f), metal).transform.localRotation = Quaternion.Euler(0f, 0f, -18f);
Primitive("Tripé_D", PrimitiveType.Cylinder, guincho.transform, new Vector3(0.55f, 1.1f, 0f), new Vector3(0.1f, 1.3f, 0.1f), metal).transform.localRotation = Quaternion.Euler(0f, 0f, 18f);
Primitive("Carretel", PrimitiveType.Cylinder, guincho.transform, new Vector3(0f, 2.1f, 0f), new Vector3(0.5f, 0.35f, 0.5f), metal).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
Placement("Encaixe_Isca", items["isca_improvisada"], guincho.transform, new Vector3(-0.45f, 1.65f, -0.45f), new Vector3(0.25f, 0.25f, 0.08f), red, Vector3.zero, Vector3.zero, Vector3.one * 0.35f);
Placement("Encaixe_Ima", items["ima"], guincho.transform, new Vector3(0.45f, 1.65f, -0.45f), new Vector3(0.25f, 0.25f, 0.08f), red, Vector3.zero, Vector3.zero, Vector3.one * 0.35f);
Pickup("Isca_Improvisada", items["isca_improvisada"], guincho.transform, new Vector3(0f, 0.4f, -0.8f), PrimitiveType.Cylinder, new Vector3(0.3f, 0.12f, 0.3f), gold, false);
Pickup("Envelope_do_Breu", items["envelope_breu"], guincho.transform, new Vector3(-0.5f, 0.3f, -1f), PrimitiveType.Cube, new Vector3(0.7f, 0.06f, 0.45f), ivory, false);
Pickup("Relogio_de_Bolso", items["relogio_bolso"], guincho.transform, new Vector3(0.5f, 0.3f, -1f), PrimitiveType.Cylinder, new Vector3(0.3f, 0.08f, 0.3f), gold, false);

GameObject cadeira = Empty("P5_Cadeira_Eletrica", Point(points4, 5).transform, Vector3.zero);
Primitive("Assento", PrimitiveType.Cube, cadeira.transform, new Vector3(0f, 0.65f, 0f), new Vector3(1.1f, 0.22f, 1.1f), wood);
Primitive("Encosto", PrimitiveType.Cube, cadeira.transform, new Vector3(0f, 1.75f, 0.45f), new Vector3(1.1f, 2.2f, 0.22f), wood);
Primitive("Braco_E", PrimitiveType.Cube, cadeira.transform, new Vector3(-0.7f, 1.05f, 0f), new Vector3(0.22f, 0.18f, 1.15f), metal);
Primitive("Braco_D", PrimitiveType.Cube, cadeira.transform, new Vector3(0.7f, 1.05f, 0f), new Vector3(0.22f, 0.18f, 1.15f), metal);
Primitive("Correias", PrimitiveType.Cube, cadeira.transform, new Vector3(0f, 1.7f, 0.28f), new Vector3(1.18f, 0.22f, 0.08f), red);

GameObject porta = Empty("P6_Porta_da_Camara", Point(points4, 6).transform, Vector3.zero);
Primitive("Breu", PrimitiveType.Cube, porta.transform, new Vector3(1.8f, 2.5f, 0f), new Vector3(0.15f, 5f, 3.5f), black);
GameObject folhaPorta = Primitive("Porta_Aberta", PrimitiveType.Cube, porta.transform, new Vector3(0.8f, 2.5f, 1.7f), new Vector3(0.18f, 5f, 3.3f), metal);
folhaPorta.transform.localRotation = Quaternion.Euler(0f, 55f, 0f);
Placement("Fechadura_Chave_da_Camara", items["chave_camara"], porta.transform, new Vector3(0.55f, 1.15f, -0.7f), new Vector3(0.22f, 0.3f, 0.12f), gold, Vector3.zero, new Vector3(0f, 90f, 0f), new Vector3(0.2f, 0.2f, 0.55f));
Pickup("Pao", items["pao"], Point(points4, 7).transform, new Vector3(0f, 0.22f, 0f), PrimitiveType.Sphere, new Vector3(0.55f, 0.3f, 0.38f), gold);
GameObject sombra = Primitive("Sombra_de_Tomas", PrimitiveType.Capsule, p4.transform, new Vector3(0f, 1f, 2.2f), new Vector3(0.65f, 1f, 0.3f), black);
sombra.SetActive(false);
GameObject tomas = Primitive("Tomas_Final", PrimitiveType.Capsule, p4.transform, new Vector3(0f, 1f, 1.4f), new Vector3(0.65f, 1f, 0.65f), ivory);
tomas.SetActive(false);

// The apple belongs to the envelope fished from the Breu and starts hidden.
GameObject envelopeBreu = guincho.transform.Find("Envelope_do_Breu").gameObject;
Pickup("Maca", items["maca"], envelopeBreu.transform, new Vector3(0f, 0.8f, 0f), PrimitiveType.Sphere, Vector3.one * 0.35f, red, false);

// Only the first moment is visible until the blink system is implemented.
p1.SetActive(true);
p2.SetActive(false);
p3.SetActive(false);
p4.SetActive(false);

Selection.activeGameObject = master;
EditorUtility.SetDirty(master);
AssetDatabase.SaveAssets();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

return new
{
    scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
    root = master.name,
    sceneGroups = 4,
    inventoryItems = items.Count,
    activeGroup = p1.name
};
}
}
