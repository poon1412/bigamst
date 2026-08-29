using System;
using System.Collections.Generic;
using BigAmbitions.ModsInternal;
using Localizor.LanguageChangeEvent;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BigAmbitionsTrainerPlus
{
    /// <summary>
    /// Works around a bug in the game's mod options UI.
    ///
    /// ModOptionsButtonControl.Initialize assigns the ButtonOption's Label to the row
    /// label but never sets the button's own text, so every mod button in the game keeps
    /// its prefab placeholder ("YOUR TEXT HERE"). Nothing passed through AddButton can
    /// change that.
    ///
    /// This sweeps the spawned controls and rewrites the button caption. It is purely
    /// cosmetic and entirely best-effort: any failure leaves the placeholder in place
    /// rather than breaking the panel.
    /// </summary>
    internal static class ButtonLabelFixer
    {
        /// <summary>Caption written onto every mod option button.</summary>
        private const string Caption = "Apply";

        /// <summary>
        /// The prefab's placeholder, as stored. The panel renders it in caps through TMP
        /// styling, so the value on disk is sentence case — compare case-insensitively.
        /// Buttons carrying any other caption are real UI and must be left alone.
        /// </summary>
        private const string Placeholder = "Your text here";

        /// <summary>
        /// Buttons already dealt with. The panel destroys and respawns its controls on
        /// every rebuild, so this is keyed on the instance and pruned as entries die.
        /// </summary>
        private static readonly HashSet<int> Handled = new HashSet<int>();

        private static bool _failed;

        /// <summary>
        /// Rewrites any button caption still showing the placeholder. Safe to call
        /// repeatedly; it does nothing once every visible button has been handled.
        /// </summary>
        internal static void Sweep()
        {
            if (_failed)
            {
                return;
            }

            try
            {
                ModOptionsButtonControl[] controls =
                    UnityEngine.Object.FindObjectsOfType<ModOptionsButtonControl>();

                if (controls.Length == 0)
                {
                    // Panel is closed. Drop the cache so the next open starts clean.
                    if (Handled.Count > 0)
                    {
                        Handled.Clear();
                    }

                    return;
                }


                foreach (ModOptionsButtonControl control in controls)
                {
                    if (control == null || !Handled.Add(control.GetInstanceID()))
                    {
                        continue;
                    }

                    Apply(control);
                }
            }
            catch (Exception exception)
            {
                // A UI cosmetic is never worth spamming the log or risking the tick, so
                // report once and stay out of the way from then on.
                _failed = true;
                Debug.LogWarning($"[Trainer Plus] Button relabelling disabled: {exception.Message}");
            }
        }

        private static void Apply(ModOptionsButtonControl control)
        {
            Button button = control.GetComponentInChildren<Button>(includeInactive: true);
            if (button == null)
            {
                return;
            }

            foreach (TMP_Text caption in button.GetComponentsInChildren<TMP_Text>(includeInactive: true))
            {
                // Only ever touch untouched placeholders. Other buttons in the same panel
                // carry real captions ("Reset windows") and must be left alone.
                if (!string.Equals(caption.text, Placeholder, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // The game drives this text through a localization component, which would
                // overwrite whatever is set here on its next refresh.
                var localized = caption.GetComponent<TextLocalizationComponent>();
                if (localized != null)
                {
                    localized.Key = Caption;
                    localized.enabled = false;
                }

                caption.text = Caption;
            }
        }
    }
}
