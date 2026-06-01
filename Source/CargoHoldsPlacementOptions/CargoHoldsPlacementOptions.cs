using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace CargoHoldsPlacementOptions
{
    public class CargoHoldsPlacementSettings : ModSettings
    {
        public bool adjacencyUseGlobal = true;
        public bool adjacencyGlobalRemove = false;
        public Dictionary<string, bool> adjacencyByDef = new Dictionary<string, bool>();

        public bool substructureUseGlobal = true;
        public bool substructureGlobalRemove = false;
        public Dictionary<string, bool> substructureByDef = new Dictionary<string, bool>();

        private List<string> adjacencyKeys;
        private List<bool> adjacencyValues;
        private List<string> substructureKeys;
        private List<bool> substructureValues;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref adjacencyUseGlobal, "adjacencyUseGlobal", true);
            Scribe_Values.Look(ref adjacencyGlobalRemove, "adjacencyGlobalRemove", false);
            Scribe_Collections.Look(ref adjacencyByDef, "adjacencyByDef", LookMode.Value, LookMode.Value, ref adjacencyKeys, ref adjacencyValues);

            Scribe_Values.Look(ref substructureUseGlobal, "substructureUseGlobal", true);
            Scribe_Values.Look(ref substructureGlobalRemove, "substructureGlobalRemove", false);
            Scribe_Collections.Look(ref substructureByDef, "substructureByDef", LookMode.Value, LookMode.Value, ref substructureKeys, ref substructureValues);

            adjacencyByDef ??= new Dictionary<string, bool>();
            substructureByDef ??= new Dictionary<string, bool>();

            base.ExposeData();
        }
    }

    public class CargoHoldsPlacementMod : Mod
    {
        public static CargoHoldsPlacementSettings Settings;
        private Vector2 scrollPosAdjacency;
        private Vector2 scrollPosSubstructure;
        private int selectedTab;

        private static List<ThingDef> cachedCargoHoldDefs;
        private static HashSet<string> cachedReplacingKeys;

        public CargoHoldsPlacementMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<CargoHoldsPlacementSettings>();
        }

        public override string SettingsCategory() => "Cargo Holds Placement Options";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var tabArea = new Rect(inRect.x, inRect.y, inRect.width, 32f);
            var contentArea = new Rect(inRect.x, inRect.y + 40f, inRect.width, inRect.height - 40f);
            var tabWidth = 160f;

            if (Widgets.ButtonText(new Rect(tabArea.x, tabArea.y, tabWidth, 30f), "Adjacency"))
                selectedTab = 0;

            if (Widgets.ButtonText(new Rect(tabArea.x + tabWidth + 8f, tabArea.y, tabWidth, 30f), "Substructure"))
                selectedTab = 1;

            if (selectedTab == 0)
                DrawAdjacencyTab(contentArea);
            else
                DrawSubstructureTab(contentArea);
        }

        private void DrawAdjacencyTab(Rect rect)
        {
            var listing = new Listing_Standard();
            listing.Begin(new Rect(rect.x, rect.y, rect.width, 90f));

            listing.CheckboxLabeled("Use one setting for all Cargo Holds", ref Settings.adjacencyUseGlobal);

            if (Settings.adjacencyUseGlobal)
                listing.CheckboxLabeled("Remove adjacency restriction for all Cargo Holds", ref Settings.adjacencyGlobalRemove);
            else
                listing.Label("Configure adjacency per item below.");

            listing.End();

            if (!Settings.adjacencyUseGlobal)
            {
                var defs = GetAdjacencyDefs();
                var outRect = new Rect(rect.x, rect.y + 96f, rect.width, rect.height - 96f);
                var viewRect = new Rect(0f, 0f, outRect.width - 16f, defs.Count * 36f + 10f);

                Widgets.BeginScrollView(outRect, ref scrollPosAdjacency, viewRect);
                var y = 0f;

                foreach (var def in defs)
                {
                    var rowRect = new Rect(0f, y, viewRect.width, 34f);
                    DrawDefToggleRow(rowRect, def, Settings.adjacencyByDef, "adjacency");
                    y += 36f;
                }

                Widgets.EndScrollView();
            }
        }

        private void DrawSubstructureTab(Rect rect)
        {
            var listing = new Listing_Standard();
            listing.Begin(new Rect(rect.x, rect.y, rect.width, 90f));

            listing.CheckboxLabeled("Use one setting for all substructure Cargo Holds", ref Settings.substructureUseGlobal);

            if (Settings.substructureUseGlobal)
                listing.CheckboxLabeled("Remove substructure requirement for all applicable Cargo Holds", ref Settings.substructureGlobalRemove);
            else
                listing.Label("Configure substructure behavior per item below.");

            listing.End();

            if (!Settings.substructureUseGlobal)
            {
                var defs = GetSubstructureDefs();
                var outRect = new Rect(rect.x, rect.y + 96f, rect.width, rect.height - 96f);
                var viewRect = new Rect(0f, 0f, outRect.width - 16f, defs.Count * 36f + 10f);

                Widgets.BeginScrollView(outRect, ref scrollPosSubstructure, viewRect);
                var y = 0f;

                foreach (var def in defs)
                {
                    var rowRect = new Rect(0f, y, viewRect.width, 34f);
                    DrawDefToggleRow(rowRect, def, Settings.substructureByDef, "substructure");
                    y += 36f;
                }

                Widgets.EndScrollView();
            }
        }

        private static void DrawDefToggleRow(Rect rowRect, ThingDef def, Dictionary<string, bool> dict, string kind)
        {
            if (def == null || dict == null) return;

            var iconRect = new Rect(rowRect.x, rowRect.y + 2f, 30f, 30f);
            var labelRect = new Rect(rowRect.x + 36f, rowRect.y + 3f, rowRect.width - 180f, rowRect.height);
            var toggleRect = new Rect(rowRect.xMax - 32f, rowRect.y + 5f, 24f, 24f);

            if (def.uiIcon != null)
                GUI.DrawTexture(iconRect, def.uiIcon, ScaleMode.ScaleToFit, true);
            else
                Widgets.DrawBoxSolid(iconRect, new Color(0.2f, 0.2f, 0.2f, 0.25f));

            string tipText = def.LabelCap.ToString();
            if (string.IsNullOrWhiteSpace(tipText))
                tipText = def.defName ?? kind;

            TooltipHandler.TipRegion(iconRect, new TipSignal(tipText));

            var key = def.defName;
            if (string.IsNullOrEmpty(key)) return;

            var value = dict.TryGetValue(key, out var existing) && existing;

            Widgets.Label(labelRect, def.LabelCap);
            Widgets.Checkbox(toggleRect.x, toggleRect.y, ref value);

            dict[key] = value;
        }

        private static bool IsCargoHoldDef(ThingDef def)
        {
            if (def == null || string.IsNullOrWhiteSpace(def.defName)) return false;
            return def.defName.IndexOf("CargoHold", StringComparison.OrdinalIgnoreCase) >= 0
                || def.defName.IndexOf("CargoComp", StringComparison.OrdinalIgnoreCase) >= 0
                || def.defName.IndexOf("CargoHoldVB", StringComparison.OrdinalIgnoreCase) >= 0
                || def.defName.IndexOf("XMCH", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsPlaceholderVariant(ThingDef def)
        {
            if (def == null) return false;
            var n = def.defName ?? string.Empty;
            return n.IndexOf("Blueprint", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Frame", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Destroyed", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsReplacingVariant(ThingDef def)
        {
            if (def == null) return false;
            var n = def.defName ?? string.Empty;
            var label = def.label ?? string.Empty;
            return n.IndexOf("Replacing", StringComparison.OrdinalIgnoreCase) >= 0
                || label.IndexOf("Replacing", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string NormalizeKey(ThingDef def)
        {
            if (def == null) return string.Empty;
            return NormalizeDefName(def.defName);
        }

        private static string NormalizeDefName(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return string.Empty;

            var n = defName.Replace("_", string.Empty);
            n = StripPrefix(n, "jdgg");
            n = StripSuffix(n, "Building");
            n = StripSuffix(n, "Blueprint");
            n = StripSuffix(n, "Frame");
            n = StripSuffix(n, "Replacing");
            n = StripSuffix(n, "Destroyed");
            n = NormalizeFamilyAliases(n);

            return n.Trim().ToLowerInvariant();
        }

        private static string StripPrefix(string text, string prefix)
        {
            return text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? text.Substring(prefix.Length) : text;
        }

        private static string StripSuffix(string text, string suffix)
        {
            return text.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ? text.Substring(0, text.Length - suffix.Length) : text;
        }

        private static string NormalizeFamilyAliases(string text)
        {
            var n = text;
            n = n.Replace("n2x2", "2x2");
            n = n.Replace("n3x3", "3x3");
            n = n.Replace("nMass", "Mass");
            n = n.Replace("nRef", "Ref");
            return n;
        }

        private static HashSet<string> GetReplacingKeys()
        {
            if (cachedReplacingKeys != null) return cachedReplacingKeys;

            cachedReplacingKeys = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(IsCargoHoldDef)
                .Where(IsReplacingVariant)
                .Select(NormalizeKey)
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return cachedReplacingKeys;
        }

        private static List<ThingDef> GetCargoHoldDefs()
        {
            if (cachedCargoHoldDefs != null) return cachedCargoHoldDefs;

            var replacingKeys = GetReplacingKeys();

            cachedCargoHoldDefs = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(IsCargoHoldDef)
                .Where(IsPlaceableBuilding)
                .Where(d => !IsPlaceholderVariant(d))
                .Where(d => !replacingKeys.Contains(NormalizeKey(d)))
                .OrderBy(d => d.label ?? d.defName)
                .ToList();

            return cachedCargoHoldDefs;
        }

        private static bool IsPlaceableBuilding(ThingDef def)
        {
            if (def == null) return false;
            if (def.category != ThingCategory.Building) return false;
            if (def.building == null) return false;
            return true;
        }

        private static List<ThingDef> GetAdjacencyDefs()
        {
            return GetCargoHoldDefs();
        }

        private static List<ThingDef> GetSubstructureDefs()
        {
            return GetCargoHoldDefs();
        }

        public static bool ShouldRemoveAdjacency(ThingDef def)
        {
            if (def == null || !IsCargoHoldDef(def) || Settings == null) return false;
            return Settings.adjacencyUseGlobal
                ? Settings.adjacencyGlobalRemove
                : Settings.adjacencyByDef.TryGetValue(def.defName, out var enabled) && enabled;
        }

        public static bool ShouldRemoveSubstructure(ThingDef def)
        {
            if (def == null || !IsCargoHoldDef(def) || Settings == null) return false;
            return Settings.substructureUseGlobal
                ? Settings.substructureGlobalRemove
                : Settings.substructureByDef.TryGetValue(def.defName, out var enabled) && enabled;
        }
    }

    [StaticConstructorOnStartup]
    public static class CargoHoldsPlacementBootstrap
    {
        static CargoHoldsPlacementBootstrap()
        {
            var harmony = new Harmony("local.cargoholdsplacementoptions");
            harmony.PatchAll();
        }
    }

    [HarmonyPatch(typeof(PlaceWorker_NeverAdjacentSameDef), nameof(PlaceWorker_NeverAdjacentSameDef.AllowsPlacing))]
    public static class Patch_PlaceWorker_NeverAdjacentSameDef
    {
        public static bool Prefix(BuildableDef __0, ref AcceptanceReport __result)
        {
            if (__0 is ThingDef thingDef && CargoHoldsPlacementMod.ShouldRemoveAdjacency(thingDef))
            {
                __result = true;
                return false;
            }
            return true;
        }
    }

#pragma warning disable 0618
    [HarmonyPatch(typeof(PlaceWorker_OnSubstructure), nameof(PlaceWorker_OnSubstructure.AllowsPlacing))]
    public static class Patch_PlaceWorker_OnSubstructure
    {
        public static bool Prefix(BuildableDef __0, ref AcceptanceReport __result)
        {
            if (__0 is ThingDef thingDef && CargoHoldsPlacementMod.ShouldRemoveSubstructure(thingDef))
            {
                __result = true;
                return false;
            }
            return true;
        }
    }
#pragma warning restore 0618
}