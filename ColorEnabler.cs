using BepInEx;
using BepInEx.IL2CPP;
using HarmonyLib;
using TMPro;
using UnityEngine;
using System.Text.RegularExpressions;

namespace Antro.ColorEnabler
{
    [BepInPlugin("Antro.ColorEnabler", "Color Enabler", "1.0.0.0")]
    public class Plugin : BasePlugin
    {
        public override void Load()
        {
            // Initialize Harmony to apply patches to the game code
            Harmony.CreateAndPatchAll(typeof(Patches));
            Log.LogInfo("Color Enabler Loaded! (>color=RED>Word wraps only the word). Size tags are disabled for security.");
        }
    }

    /// <summary>
    /// This class contains Harmony patches that modify how TextMeshPro (TMP) handles text.
    /// It forces Rich Text to be enabled globally and introduces a custom, shorthand syntax for coloring words.
    /// </summary>
    public static class Patches
    {
        // ==================================================================================
        // 1. FORCE RICH TEXT ENABLED
        // These patches ensure that any text element created in the game (UI or 3D World)
        // supports HTML-like tags (e.g., <color=red>, <b>, <i>) by default.
        // ==================================================================================

        /// <summary>
        /// Patches the Awake method of UI Text elements (2D).
        /// Forces the 'richText' boolean to true immediately upon creation.
        /// </summary>
        [HarmonyPatch(typeof(TextMeshProUGUI), "Awake")]
        [HarmonyPostfix]
        public static void ForceRichTextUI(TextMeshProUGUI __instance)
        {
            if (__instance != null) __instance.richText = true;
        }

        /// <summary>
        /// Patches the Awake method of World Text elements (3D).
        /// Forces the 'richText' boolean to true immediately upon creation.
        /// </summary>
        [HarmonyPatch(typeof(TextMeshPro), "Awake")]
        [HarmonyPostfix]
        public static void ForceRichText3D(TextMeshPro __instance)
        {
            if (__instance != null) __instance.richText = true;
        }

        // ==================================================================================
        // 2. TEXT PROCESSING & SANITIZATION
        // This patch intercepts the text *before* it is assigned to the text component.
        // It removes malicious tags (like size) and parses our custom shorthand syntax.
        // ==================================================================================

        [HarmonyPatch(typeof(TMP_Text), "set_text")]
        [HarmonyPrefix]
        public static void ReplaceTags(ref string value)
        {
            // If the string is null or empty, there is nothing to process.
            if (string.IsNullOrEmpty(value)) return;

            // --- SECURITY: DISABLE SIZE TAGS ---
            // Malicious users often use <size=10000> to create giant text that blocks the screen.
            // We use Regex to completely strip any <size> tags before the game renders them.
            // pattern: (?i) = case insensitive
            //          <size=  = matches the opening tag
            //          [^>]*   = matches any character until the closing bracket
            //          >       = closing bracket
            if (value.IndexOf("size", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                value = Regex.Replace(value, @"(?i)<size=[^>]*>", string.Empty);
                value = Regex.Replace(value, @"(?i)</size>", string.Empty);
            }

            // --- CUSTOM SYNTAX PARSING ---

            // Check if the text contains our custom color tag format: >color=COLOR>Word
            if (value.Contains(">color="))
            {
                // REGEX EXPLANATION:
                // >color=      -> Matches the start of our custom tag.
                // ([^>]+)      -> Group 1: Captures the color name/hex (everything until the next '>').
                // >            -> Matches the separator between color and text.
                // (\S+)        -> Group 2: Captures the single WORD (everything until whitespace).

                string pattern = @">color=([^>]+)>(\S+)";

                // REPLACEMENT:
                // <color=$1>   -> Opens standard TMP tag with the captured color (Group 1).
                // $2           -> Inserts the captured word (Group 2).
                // </color>     -> Immediately closes the tag so only that word is colored.

                string replacement = @"<color=$1>$2</color>";

                // Execute the replacement
                value = Regex.Replace(value, pattern, replacement);
            }

            // Check if the text contains our custom bold tag format: >b>Word
            if (value.Contains(">b>"))
            {
                // Converts >b>Word into <b>Word</b>
                // Useful for quickly highlighting specific words in chat or nicknames.
                value = Regex.Replace(value, @">b>(\S+)", @"<b>$1</b>");
            }

            // Note: If the player wants to color an entire sentence (including spaces),
            // they can still use the standard Unity syntax: <color=red>Full Sentence</color>.
            // Our logic only affects the shorthand syntax for single words.
        }
    }
}