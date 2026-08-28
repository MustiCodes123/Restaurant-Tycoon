using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class ReadyCharacterImporter : AssetPostprocessor
{
    private const string ModelsFolder = "Assets/Ready Characters/Models";
    private const string ReportPath = "Library/ReadyCharacterValidation.txt";

    private static readonly string[] ExpectedCharacters =
    {
        "CH_1_11_C_MC",
        "CH_2_1_A_MC",
        "CH_3_7_B_MC",
        "CH_8_2_B_MC",
        "CH_8_3_A_MC",
        "CH_8_3_B_MC",
        "CH_8_3_C_MC",
        "CH_8_3_E_MC",
        "CH_9_3_C_MC",
        "CH_9_4_C_MC",
        "CH_11_8_B_MC",
        "CH_12_3_C_MC",
        "CH_12_5_C_MC",
        "CH_14_1_C_MC",
    };

    private void OnPreprocessModel()
    {
        if (!IsReadyCharacter(assetPath))
            return;

        ModelImporter importer = (ModelImporter)assetImporter;
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = false;
        importer.optimizeGameObjects = false;
    }

    [MenuItem("Tools/Characters/Reimport And Validate Ready Characters")]
    public static void ReimportAndValidate()
    {
        List<string> modelPaths = GetExpectedModelPaths();
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string path in modelPaths.Where(File.Exists))
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ValidateModels(modelPaths);
    }

    [MenuItem("Tools/Characters/Validate Ready Characters")]
    public static void ValidateReadyCharacters()
    {
        ValidateModels(GetExpectedModelPaths());
    }

    private static void ValidateModels(IReadOnlyList<string> modelPaths)
    {
        List<string> report = new List<string>
        {
            $"Ready character validation - {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            string.Empty,
        };

        int passed = 0;
        foreach (string path in modelPaths)
        {
            string characterName = Path.GetFileNameWithoutExtension(path);
            if (!File.Exists(path))
            {
                report.Add($"FAIL | {characterName} | model file is missing");
                continue;
            }

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            Transform hips = model != null ? FindChild(model.transform, "Hips") : null;
            Transform torso = model != null ? FindChild(model.transform, "Torso") : null;
            Transform leftLeg = model != null ? FindChild(model.transform, "UpperLeg.L") : null;
            Transform rightLeg = model != null ? FindChild(model.transform, "UpperLeg.R") : null;

            bool hierarchyValid = hips != null &&
                                  torso != null && torso.parent == hips &&
                                  leftLeg != null && leftLeg.parent == hips &&
                                  rightLeg != null && rightLeg.parent == hips;
            bool avatarValid = avatar != null && avatar.isValid && avatar.isHuman;
            bool passedModel = model != null && hierarchyValid && avatarValid;

            report.Add(
                $"{(passedModel ? "PASS" : "FAIL")} | {characterName} | " +
                $"hierarchy={hierarchyValid} | avatarValid={avatarValid} | " +
                $"avatar={(avatar != null ? avatar.name : "missing")}");

            if (passedModel)
                passed++;
        }

        report.Add(string.Empty);
        report.Add($"RESULT | {passed}/{modelPaths.Count} characters passed");
        File.WriteAllLines(ReportPath, report);

        string summary = string.Join("\n", report);
        if (passed == modelPaths.Count)
            Debug.Log(summary);
        else
            Debug.LogError(summary);
    }

    private static List<string> GetExpectedModelPaths()
    {
        return ExpectedCharacters
            .Select(name => $"{ModelsFolder}/{name}/{name}.fbx")
            .ToList();
    }

    private static bool IsReadyCharacter(string path)
    {
        return path.StartsWith(ModelsFolder + "/", StringComparison.Ordinal) &&
               path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);
    }

    private static Transform FindChild(Transform root, string childName)
    {
        if (root.name == childName)
            return root;

        foreach (Transform child in root)
        {
            Transform found = FindChild(child, childName);
            if (found != null)
                return found;
        }

        return null;
    }
}
