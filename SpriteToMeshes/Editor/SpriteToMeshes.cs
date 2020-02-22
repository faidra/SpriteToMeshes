using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class SpriteToMeshes
{
    const string path = "Assets/SpriteToMeshes";

    [MenuItem(path, true)]
    static bool Validate()
    {
        return Selection.activeObject is Texture2D || Selection.activeObject is Sprite;
    }

    [MenuItem(path)]
    static void Execute()
    {
        var path = AssetDatabase.GetAssetPath(Selection.activeObject);
        var selectingAssets = AssetDatabase.LoadAllAssetsAtPath(path);
        var sprites = selectingAssets.OfType<Sprite>().ToArray();
        if (sprites.Length == 0)
        {
            EditorUtility.DisplayDialog("エラー", "Spriteを選択して実行してください", "OK");
            return;
        }
        var sourceTextures = selectingAssets.OfType<Texture2D>();
        if (sourceTextures.Count() != 1)
        {
            EditorUtility.DisplayDialog("エラー", "Textureに紐づくSpriteを選択して実行してください", "OK"); // 状況が分からん
            return;
        }
        var sourceTexture = sourceTextures.First();
        if (sprites.Any(s => s.texture != sourceTexture))
        {
            EditorUtility.DisplayDialog("エラー", "Textureに紐づくSpriteを選択して実行してください", "OK"); // そんな事するやつおる？
            return;
        }

        var textureName = sourceTexture.name;
        var directoryPath = Path.Combine(Path.GetDirectoryName(path), textureName + "Meshes");
        if (!Directory.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);

        var materialName = textureName + "Material";
        var materialPath = Path.Combine(directoryPath, materialName + ".mat");
        var material = GetOrCreateMaterial(materialPath, sourceTexture);

        foreach (var sprite in sprites)
        {
            Vector3 position;
            var mesh = GetOrCreateMesh(Path.Combine(directoryPath, sprite.name + "Mesh" + ".asset"));
            ConfigureMeshFromSprite(sprite, mesh, out position);
            var prefabPath = Path.Combine(directoryPath, sprite.name + ".prefab");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) prefab = CreateSimplePrefab(prefabPath, sprite.name);
            ConfigureGameObject(prefab, mesh, material, position);
#if UNITY_2018_3_OR_NEWER
            PrefabUtility.SavePrefabAsset(prefab);
#endif
        }
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("完了", directoryPath + "にPrefabを作成/更新しました", "OK");
    }

    static Material GetOrCreateMaterial(string path, Texture2D sourceTexture)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        var shaderName = UseTransparent() ? "Unlit/Transparent" : "Unlit/Texture";
        mat = new Material(Shader.Find(shaderName));
        mat.mainTexture = sourceTexture;
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static bool UseTransparent()
    {
        // α値があるか判別しようと思ったけど大変だった
        return EditorUtility.DisplayDialog("確認", "メッシュを透過させますか?", "Yes", "No");
    }

    static Mesh GetOrCreateMesh(string path)
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh != null) return mesh;
        mesh = new Mesh();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    static void ConfigureMeshFromSprite(Sprite sprite, Mesh mesh, out Vector3 position)
    {
        mesh.name = sprite.name;
        var longest = Mathf.Max(sprite.texture.width, sprite.texture.height);
        var center = (sprite.rect.min + sprite.pivot) / longest;
        var offset = new Vector2(sprite.texture.width, sprite.texture.height) / longest * 0.5f;
        position = center - offset;
        var scale = sprite.pixelsPerUnit / longest;
        mesh.Clear();
        mesh.SetVertices(sprite.vertices
            .Select(p => p * scale)
            .Select(v => (Vector3)v)
            .ToList());
        mesh.SetUVs(0, sprite.uv.ToList());
        mesh.SetTriangles(Array.ConvertAll(sprite.triangles, c => (int)c), 0);
        EditorUtility.SetDirty(mesh);
    }

    static void ConfigureGameObject(GameObject gameObject, Mesh mesh, Material material, Vector3 position)
    {
        gameObject.transform.position = position;
        gameObject.GetOrAddComponent<MeshFilter>().sharedMesh = mesh;
        gameObject.GetOrAddComponent<MeshRenderer>().sharedMaterial = material;
        EditorUtility.SetDirty(gameObject);
    }

    static GameObject CreateSimplePrefab(string path, string name)
    {
        // いい方法ないんですか
        var newGameObject = new GameObject(name);
#if UNITY_2018_3_OR_NEWER
        PrefabUtility.SaveAsPrefabAsset(newGameObject, path);
#else
        PrefabUtility.CreatePrefab(path.Replace('\\', '/'), newGameObject);
#endif
        GameObject.DestroyImmediate(newGameObject);
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    static T GetOrAddCompononent<T>(GameObject gameObject)where T : Component
    {
        var exists = gameObject.GetComponent<T>();
        if (exists) return exists;
        return gameObject.AddComponent<T>();
    }
}
