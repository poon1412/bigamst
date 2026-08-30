using System;
using BigAmbitions.Mods;
using BigAmbitions.ModsInternal;
using Localizor.LanguageChangeEvent;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BigamstTrainer
{
    /// <summary>
    /// Works around a bug in the game's mod options UI.
    ///
    /// ModOptionsButtonControl.Initialize assigns the ButtonOption's Label to the row
    /// label but never sets the button's own text, so every mod button in the game keeps
    /// its prefab placeholder ("Your text here"). Nothing passed through AddButton can
    /// change that.
    ///
    /// This does no polling. <see cref="HookOption"/> is registered last in the options
    /// list, so the game calls its SpawnUi at the end of every panel rebuild, which is
    /// precisely when new buttons exist and the only time relabelling is needed.
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

        /// <summary>Root the panel spawned our options into, captured on rebuild.</summary>
        private static Transform _pendingRoot;

        private static bool _failed;

        /// <summary>
        /// Add this to the end of the options list. The game calls SpawnUi on it during
        /// ModOptionsViewController.Rebuild, giving a free notification that the panel
        /// was just built — no scene scanning and no per-frame cost.
        /// </summary>
        internal sealed class HookOption : ModOption
        {
            internal HookOption()
                : base(null, string.Empty)
            {
            }

            public override void SpawnUi(Transform parent, string modId)
            {
                // Deliberately renders nothing. It exists only for this callback.
                _pendingRoot = parent;
            }
        }

        internal static void Reset()
        {
            _pendingRoot = null;
            _failed = false;
        }

        /// <summary>
        /// Relabels buttons if the panel was rebuilt since the last call. Costs two field
        /// reads when there is nothing to do, which is every frame outside the menu.
        /// </summary>
        internal static void ProcessPendingRebuild()
        {
            if (_pendingRoot == null || _failed)
            {
                return;
            }

            Transform root = _pendingRoot;
            _pendingRoot = null;
            RelabelUnder(root);
        }

        /// <summary>
        /// Relabels every option button beneath <paramref name="root"/>. The phone panel
        /// spawns its own controls, so it has to ask for this explicitly rather than
        /// relying on the options panel rebuild hook.
        /// </summary>
        internal static void RelabelUnder(Transform root)
        {
            if (root == null || _failed)
            {
                return;
            }

            try
            {
                // Scoped to the given content root, never the whole scene.
                foreach (ModOptionsButtonControl control in
                         root.GetComponentsInChildren<ModOptionsButtonControl>(includeInactive: true))
                {
                    Apply(control);
                }
            }
            catch (Exception exception)
            {
                // A UI cosmetic is never worth risking the tick, so report once and stay
                // out of the way from then on.
                _failed = true;
                Debug.LogWarning($"[Bigamst Trainer] Button relabelling disabled: {exception.Message}");
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
