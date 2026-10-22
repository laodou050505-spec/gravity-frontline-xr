#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

public static class ApplyPicoIcon
{
    private const string IconPath = "Assets/AppIcon/AppIcon.png";

    public static void Run()
    {
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        if (icon == null) throw new System.Exception("Missing AppIcon");

        // Android 15 / PICO launchers choose from the adaptive and round icon slots
        // before the legacy application slot. Populate every official Android slot
        // from this single square artwork so the launcher cannot fall back to Unity.
        var target = NamedBuildTarget.Android;
        foreach (var kind in PlayerSettings.GetSupportedIconKinds(target))
        {
            var slots = PlayerSettings.GetPlatformIcons(target, kind);
            foreach (var slot in slots)
            {
                var layers = new Texture2D[slot.maxLayerCount];
                for (var layer = 0; layer < layers.Length; layer++) layers[layer] = icon;
                slot.SetTextures(layers);
            }
            PlayerSettings.SetPlatformIcons(target, kind, slots);
            Debug.Log($"PICO_ICON_SLOT_APPLIED kind={kind} slots={slots.Length}");
        }

        PlayerSettings.SetIcons(target, new[] { icon }, IconKind.Application);
        AssetDatabase.SaveAssets();
        Debug.Log("PICO_ICON_APPLIED artwork=AppIcon.png allAndroidSlots=true");
    }
}
#endif
