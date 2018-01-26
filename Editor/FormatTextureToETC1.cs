using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using System.IO;
using System.Reflection;

public class FormatTextureToETC1
{

    private static string defaultWhiteTexPath_relative = "Assets/Default_Alpha.png";
    private static Texture2D defaultWhiteTex = null;

    static string[] PATHS = { Application.dataPath + "/Atlas/" };

    [MenuItem("ETC1/Depart RGB and Alpha Channel")]
    static void SeperateAllTexturesRGBandAlphaChannel()
    {
#if !UNITY_ANDROID
    Debug.Log("当前不是Android环境！");
    return;
#endif

        Debug.Log("Start Departing.");
       
        List<string> paths = new List<string>();
        for(int i = 0; i < PATHS.Length; ++i)
        {
            paths.AddRange(Directory.GetFiles(PATHS[i], "*.*", SearchOption.AllDirectories));
        }

        foreach (string path in paths)
        {
            if (!string.IsNullOrEmpty(path) && IsTextureFile(path) && !IsTextureConverted(path))   //full name  
            {
                SeperateRGBAandlphaChannel(path);
            }
        }
        AssetDatabase.Refresh();    //Refresh to ensure new generated RBA and Alpha textures shown in Unity as well as the meta file

        List<string> materials = new List<string>(); ;
        for (int i = 0; i < PATHS.Length; ++i)
        {
            paths.AddRange(Directory.GetFiles(PATHS[i], "*.mat", SearchOption.AllDirectories));
        }
        foreach (string path in paths)
        {
            if (!string.IsNullOrEmpty(path) && IsTextureFile(path) && IsTextureConverted(path) && IsAlphaTexture(path) ==false)   //full name  
            {
                ChangeTextureShader(materials,path);
            }
        }

        AssetDatabase.Refresh();

        Debug.Log("Finish Departing.");
    }
    [MenuItem("ETC1/Reimport AlphaTexture")]
    static void ReimportAlphaTexture()
    {
#if !UNITY_ANDROID
    Debug.Log("当前不是Android环境！");
    return;
#endif

        List<string> paths = new List<string>();
        for (int i = 0; i < PATHS.Length; ++i)
        {
            paths.AddRange(Directory.GetFiles(PATHS[i], "*.*", SearchOption.AllDirectories));
        }

        foreach (string path in paths)
        {
            if (!string.IsNullOrEmpty(path) && IsTextureFile(path) && IsTextureConverted(path))   //full name  
            {
                if(IsAlphaTexture(path))
                {
                    string assetRelativePath = GetRelativeAssetPath(path);
                    Texture2D tex = AssetDatabase.LoadAssetAtPath(assetRelativePath, typeof(Texture2D)) as Texture2D;
                    if(tex)
                    {
                        ReImportAsset(assetRelativePath, tex.width, tex.height);
                    }
                    else
                    {
                        Debug.LogError("Can not find alpha texture:" + path);
                    }
                }
            }
        }
    }

#region process texture

    static void SeperateRGBAandlphaChannel(string texPath)
    {
        string assetRelativePath = GetRelativeAssetPath(texPath);

        SetTextureReadable(assetRelativePath);    //set readable flag and set textureFormat TrueColor

        Texture2D sourcetex = AssetDatabase.LoadAssetAtPath(assetRelativePath, typeof(Texture2D)) as Texture2D;  //not just the textures under Resources file  
        if (!sourcetex)
        {
            Debug.LogError("Load Texture Failed : " + assetRelativePath);
            return;
        }

        TextureImporter ti = null;
        try
        {
            ti = (TextureImporter)TextureImporter.GetAtPath(assetRelativePath);
        }
        catch
        {
            Debug.LogError("Load Texture failed: " + assetRelativePath);
            return;
        }
        if (ti == null)
        {
            return;
        }
        bool bGenerateMipMap = ti.mipmapEnabled;    //same with the texture import setting      

        Texture2D rgbTex = new Texture2D(sourcetex.width, sourcetex.height, TextureFormat.RGB24, bGenerateMipMap);
        rgbTex.SetPixels(sourcetex.GetPixels());

        Color[] colors2rdLevel = sourcetex.GetPixels(1);   //Second level of Mipmap
        Color[] colorsAlpha = new Color[colors2rdLevel.Length];

        bool bAlphaExist = false;
        for (int i = 0; i < colors2rdLevel.Length; ++i)
        {
            colorsAlpha[i].r = colors2rdLevel[i].a;
            colorsAlpha[i].g = colors2rdLevel[i].a;
            colorsAlpha[i].b = colors2rdLevel[i].a;

            if (!Mathf.Approximately(colors2rdLevel[i].a, 1.0f))
            {
                bAlphaExist = true;
            }
        }
        Texture2D alphaTex = null;
        if (bAlphaExist)
        {
            alphaTex = new Texture2D((sourcetex.width + 1) / 2, (sourcetex.height + 1) / 2, TextureFormat.RGB24, bGenerateMipMap);
        }
        else
        {
            alphaTex = new Texture2D(defaultWhiteTex.width, defaultWhiteTex.height, TextureFormat.RGB24, false);
        }

        alphaTex.SetPixels(colorsAlpha);

        rgbTex.Apply();
        alphaTex.Apply();

        byte[] rgbbytes = rgbTex.EncodeToPNG();
        File.WriteAllBytes(assetRelativePath, rgbbytes);

        byte[] alphabytes = alphaTex.EncodeToPNG();
        string alphaTexRelativePath = GetAlphaTexPath(texPath);
        File.WriteAllBytes(alphaTexRelativePath, alphabytes);

        ReImportAsset(assetRelativePath, rgbTex.width, rgbTex.height);
        ReImportAsset(alphaTexRelativePath, alphaTex.width, alphaTex.height);
        Debug.Log("Succeed Departing : " + assetRelativePath);
    }

    public  static void ReImportAsset(string path, int width, int height)
    {
        try
        {
            AssetDatabase.ImportAsset(path);
        }
        catch
        {
            Debug.LogError("Import Texture failed: " + path);
            return;
        }

        TextureImporter importer = null;
        try
        {
            importer = (TextureImporter)TextureImporter.GetAtPath(path);
        }
        catch
        {
            Debug.LogError("Load Texture failed: " + path);
            return;
        }
        if (importer == null)
        {
            Debug.LogError("Load TextureImporter failed: " + path);
            return;
        }
        importer.maxTextureSize = Mathf.Max(width, height);
        importer.textureType = TextureImporterType.Advanced;
        importer.mipmapEnabled = false;

        importer.anisoLevel = 0;
        importer.isReadable = false;  //increase memory cost if readable is true

        if (IsAlphaTexture(path) == false)
        {
            importer.textureFormat = TextureImporterFormat.ETC2_RGB4;
            importer.compressionQuality = 50;
        }
        else
        {
            Debug.Log("Reimport Alpha");
            importer.textureFormat = TextureImporterFormat.AutomaticCompressed;
        }
       
        AssetDatabase.ImportAsset(path);
    }

    static void SetTextureReadable(string relativeAssetPath)    //set readable flag and set textureFormat TrueColor
    {
        TextureImporter ti = null;
        try
        {
            ti = (TextureImporter)TextureImporter.GetAtPath(relativeAssetPath);
        }
        catch
        {
            Debug.LogError("Load Texture failed: " + relativeAssetPath);
            return;
        }
        if (ti == null)
        {
            return;
        }
        ti.isReadable = true;
        ti.textureFormat = TextureImporterFormat.AutomaticTruecolor;      //this is essential for departing Textures for ETC1. No compression format for following operation.
        AssetDatabase.ImportAsset(relativeAssetPath);
    }

    static bool GetDefaultWhiteTexture()
    {
        defaultWhiteTex = AssetDatabase.LoadAssetAtPath(defaultWhiteTexPath_relative, typeof(Texture2D)) as Texture2D;  //not just the textures under Resources file  
        if (!defaultWhiteTex)
        {
            Debug.LogError("Load Texture Failed : " + defaultWhiteTexPath_relative);
            return false;
        }
        return true;
    }

    static void ChangeTextureShader(List<string> materials, string path)
    {
        if (materials == null || materials.Count == 0 || string.IsNullOrEmpty(path)) return;

        string assetRelativePath = GetRelativeAssetPath(path).ToLower();

        for(int i =0; i < materials.Count; ++i)
        {
            string materialPath = GetRelativeAssetPath(materials[i]);
            string[] dependences = AssetDatabase.GetDependencies(materialPath,false);

            for(int j = 0; j <dependences.Length; ++j)
            {
                if(dependences[j].ToLower()== assetRelativePath)
                {
                    Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

                    if(material)
                    {
                        Shader shader = Shader.Find("Unlit/Transparent Colored Alpha");
                        if(shader)
                        {
                            material.shader = shader;
                        }

                        Texture rgbTex = AssetDatabase.LoadAssetAtPath<Texture>(assetRelativePath);

                        if(rgbTex)
                        {
                            material.SetTexture("Base (RGB)", rgbTex);
                        }

                        Texture alphaTex = AssetDatabase.LoadAssetAtPath<Texture>(GetAlphaTexPath(assetRelativePath));

                        if(alphaTex)
                        {
                            material.SetTexture("Alpha (A)", alphaTex);
                        }
                    }

                    break;
                }
            }
        }
    }

#endregion

#region string or path helper  

    static bool IsTextureFile(string path)
    {
        path = path.ToLower();
        return path.EndsWith(".psd") || path.EndsWith(".tga") || path.EndsWith(".png") || path.EndsWith(".jpg") || path.EndsWith(".bmp") || path.EndsWith(".tif") || path.EndsWith(".gif");
    }

    static bool IsAlphaTexture(string path)
    {
        return (!string.IsNullOrEmpty(path) && path.Contains("_Alpha."));
    }

    static bool IsTextureConverted(string path)
    {
        bool converted = path.Contains("_RGB.") || path.Contains("_Alpha.");
        if(converted==false)
        {
            if(path.Contains("_Alpha.")==false)
            {
                string alphaPath = GetAlphaTexPath(path);
                if(File.Exists(alphaPath))
                {
                    converted = true;
                }
            }
        }
        return converted;
    }

    static string GetRGBTexPath(string texPath)
    {
        return GetTexPath(texPath, "_RGB.");
    }

    static string GetAlphaTexPath(string texPath)
    {
        return GetTexPath(texPath, "_Alpha.");
    }

    static string GetTexPath(string texPath, string texRole)
    {
        string dir = System.IO.Path.GetDirectoryName(texPath);
        string filename = System.IO.Path.GetFileNameWithoutExtension(texPath);
        string result = dir + "/" + filename + texRole + "png";
        return result;
    }

    static string GetRelativeAssetPath(string fullPath)
    {
        fullPath = GetRightFormatPath(fullPath);
        int idx = fullPath.IndexOf("Assets");
        string assetRelativePath = fullPath.Substring(idx);
        return assetRelativePath;
    }

    static string GetRightFormatPath(string path)
    {
        return path.Replace("\\", "/");
    }

#endregion
}
