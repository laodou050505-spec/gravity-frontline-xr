#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using System.Linq;
using UnityEngine;

namespace GravityRepulsion.Editor
{
    public static class GravityModularUiBuild
    {
        public static void Prepare()
        {
            var pico = ByteDance.PICO.XR.PXR_ProjectSetting.GetProjectConfig();
            pico.handTracking = true;
            pico.handTrackingSupportType = ByteDance.PICO.XR.HandTrackingSupport.ControllersAndHands;
            EditorUtility.SetDirty(pico); AssetDatabase.SaveAssets();
            GravityGroundUpgrade.Apply();
            GravityArchitectureUpgrade.Apply();
            GravityGroundAudit.Run();
            GravityViewFramingUpgrade.Apply();
            const string root = "Assets/GravityWarden/Resources/GravityUI";
            foreach (var file in Directory.GetFiles(root,"*.png"))
            {
                var importer = AssetImporter.GetAtPath(file) as TextureImporter;
                if (importer == null) continue;
                importer.textureType = TextureImporterType.Default;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.mipmapEnabled = false; importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear; importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            foreach (var name in new[] {"Title","Start","Exit","KeyArt","Background","BackgroundPico","JarvisVisorOverlay","VisorLabels","VisorGlassCracks","VisorCentralCracks","HudTopLeft","HudTopRight","HudBottomLeft","HudBottomRight","Idle","Locked","Holding","Ready","Impact","Danger","ResultLeft","ResultRight"})
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(root+"/"+name+".png") == null) throw new Exception("Missing generated art: " + name);
            const string visorMaterialPath = root + "/VisorOverlay.mat";
            var visorMaterial = AssetDatabase.LoadAssetAtPath<Material>(visorMaterialPath);
            if (visorMaterial == null) { visorMaterial = new Material(Shader.Find("GravityWarden/UI/GravityVisorOverlay")); AssetDatabase.CreateAsset(visorMaterial, visorMaterialPath); }
            visorMaterial.SetFloat("_KeyThreshold", .15f); visorMaterial.SetFloat("_KeySoftness", 14f); visorMaterial.SetFloat("_ScanlineStrength", .04f);
            EditorUtility.SetDirty(visorMaterial); AssetDatabase.SaveAssets();
            const string damagePath = root + "/VisorDamage.mat";
            var damageMaterial = AssetDatabase.LoadAssetAtPath<Material>(damagePath);
            if(damageMaterial == null) { damageMaterial = new Material(Shader.Find("GravityWarden/UI/VisorDamage")); AssetDatabase.CreateAsset(damageMaterial,damagePath); }
            damageMaterial.SetFloat("_Damage",0f); damageMaterial.SetFloat("_HitPulse",0f); damageMaterial.renderQueue=4990;
            damageMaterial.SetFloat("_EdgeStrength",.25f);
            EditorUtility.SetDirty(damageMaterial); AssetDatabase.SaveAssets();
            const string supplementPath = root + "/VisorSupplement.mat";
            var supplementMaterial = AssetDatabase.LoadAssetAtPath<Material>(supplementPath);
            if(supplementMaterial == null) { supplementMaterial = new Material(Shader.Find("GravityWarden/UI/VisorSupplement")); AssetDatabase.CreateAsset(supplementMaterial,supplementPath); }
            supplementMaterial.SetFloat("_Layer",0f); supplementMaterial.SetFloat("_Darkening",0f);
            supplementMaterial.SetFloat("_FaultRatio",0f); supplementMaterial.SetFloat("_HitPulse",0f);
            supplementMaterial.SetFloat("_CentralCracks",0f); supplementMaterial.renderQueue=4990;
            EditorUtility.SetDirty(supplementMaterial); AssetDatabase.SaveAssets();
            const string topPath = root + "/Topmost.mat";
            var topMaterial = AssetDatabase.LoadAssetAtPath<Material>(topPath);
            if (topMaterial == null) { topMaterial = new Material(Shader.Find("GravityWarden/UI/Topmost")); AssetDatabase.CreateAsset(topMaterial, topPath); }
            topMaterial.renderQueue = 4990;
            EditorUtility.SetDirty(topMaterial); AssetDatabase.SaveAssets();
            const string materialPath = root + "/HudKey.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) { material = new Material(Shader.Find("GravityWarden/UI/TripoHudFrameKey")); AssetDatabase.CreateAsset(material, materialPath); }
            material.SetFloat("_KeyThreshold", .11f); material.SetFloat("_KeySoftness", 7f);
            material.SetFloat("_BackgroundOpacity", .48f);
            material.renderQueue = 4990;
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssets();
            const string titleKeyPath = root + "/TitleKey.mat";
            var titleKey = AssetDatabase.LoadAssetAtPath<Material>(titleKeyPath);
            if (titleKey == null) { titleKey = new Material(Shader.Find("GravityWarden/UI/TripoHudFrameKey")); AssetDatabase.CreateAsset(titleKey, titleKeyPath); }
            titleKey.SetFloat("_KeyThreshold", .018f); titleKey.SetFloat("_KeySoftness", 28f); titleKey.SetFloat("_BackgroundOpacity", 0f);
            EditorUtility.SetDirty(titleKey); AssetDatabase.SaveAssets();
            const string featherPath = root + "/BackgroundFeather.mat";
            var feather = AssetDatabase.LoadAssetAtPath<Material>(featherPath);
            if (feather == null) { feather = new Material(Shader.Find("GravityWarden/UI/SpatialBackgroundFeather")); AssetDatabase.CreateAsset(feather, featherPath); }
            feather.SetFloat("_Feather", .20f);
            feather.SetFloat("_MinimumAlpha", 0f);
            EditorUtility.SetDirty(feather); AssetDatabase.SaveAssets();
            GravityExistingSceneUpgrader.CreateOrUpdateTitleScene();
            GravityExistingSceneUpgrader.MigrateCombatHudToCameraSpace();
            GravityExistingSceneUpgrader.ValidateExistingScene();
            Debug.Log("GRAVITY_MODULAR_UI_PREPARE_PASS resources=18 fixedText=images");
        }
        public static void PrepareAndBuildMac()
        {
            Prepare();
            var report = BuildPipeline.BuildPlayer(EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(), "Builds/macOS/GravityModularReview.app", BuildTarget.StandaloneOSX, BuildOptions.Development);
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Modular review build failed");
            Debug.Log("GRAVITY_MODULAR_REVIEW_BUILD_PASS");
        }
    }
}
#endif
