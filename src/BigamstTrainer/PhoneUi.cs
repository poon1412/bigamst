using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BigamstTrainer
{
    /// <summary>
    /// Builds the Trainer panel's controls in code.
    ///
    /// The game's UI prefabs are private SerializeFields we cannot reach, so these are
    /// plain Unity UI objects styled to sit reasonably alongside the rest of the phone.
    /// The one thing that must be borrowed is a TMP font asset — a TextMeshProUGUI with
    /// no font renders nothing at all.
    /// </summary>
    internal static class PhoneUi
    {
        private static readonly Color PanelBackground = new Color(0.11f, 0.13f, 0.16f, 0.94f);
        private static readonly Color ButtonNormal    = new Color(0.20f, 0.24f, 0.30f, 1f);
        private static readonly Color ButtonHover     = new Color(0.26f, 0.33f, 0.42f, 1f);
        private static readonly Color ButtonPressed   = new Color(0.16f, 0.20f, 0.26f, 1f);
        private static readonly Color HeadingColour   = new Color(0.62f, 0.78f, 1f, 1f);
        private static readonly Color BodyColour      = new Color(0.92f, 0.94f, 0.96f, 1f);

        /// <summary>Height of an interactive row, shared so nothing overlaps.</summary>
        private const float RowHeight = 44f;

        /// <summary>
        /// Text rows need more room than a button: the cloned field has its own padding,
        /// and the typed value has to stay legible at the size the rest of the menu uses.
        /// </summary>
        private const float InputRowHeight = 60f;

        private const float InputFontSize = 20f;

        /// <summary>Borrowed from live UI the first time it is needed.</summary>
        private static TMP_FontAsset _font;

        internal static void Forget() => _font = null;

        /// <summary>
        /// Finds a font already in use by the game's UI. Creating text without one
        /// produces an invisible label rather than an error, which is hard to diagnose.
        /// </summary>
        internal static TMP_FontAsset ResolveFont(GameObject near)
        {
            if (_font != null)
            {
                return _font;
            }

            foreach (TMP_Text text in near.GetComponentsInChildren<TMP_Text>(includeInactive: true))
            {
                if (text != null && text.font != null)
                {
                    _font = text.font;
                    return _font;
                }
            }

            return null;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);
            return (RectTransform)go.transform;
        }

        /// <summary>Creates the scrolling body of the panel and returns the content root.</summary>
        internal static RectTransform CreateScrollBody(RectTransform panel, float topInset)
        {
            RectTransform viewport = NewRect("Viewport", panel);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(30f, 20f);
            viewport.offsetMax = new Vector2(-30f, -topInset);

            Image mask = viewport.gameObject.AddComponent<Image>();
            mask.color = new Color(0f, 0f, 0f, 0.001f); // RectMask2D needs no image, Mask does
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = NewRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;

            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.padding = new RectOffset(0, 12, 0, 12);
            layout.childControlWidth = true;
            // Must be true: with it off, children keep their own height and a cloned
            // control (the text field) overlaps the row beneath it.
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = panel.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            return content;
        }

        internal static void PaintBackground(RectTransform panel)
        {
            Image image = panel.gameObject.AddComponent<Image>();
            image.color = PanelBackground;
        }

        internal static TextMeshProUGUI CreateLabel(
            Transform parent, string text, float size, Color colour, float height)
        {
            RectTransform rect = NewRect("Label", parent);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();

            if (_font != null)
            {
                label.font = _font;
            }

            label.text = text;
            label.fontSize = size;
            label.color = colour;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;

            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
            return label;
        }

        internal static void CreateHeading(Transform parent, string text) =>
            CreateLabel(parent, text.ToUpperInvariant(), 22f, HeadingColour, 40f);

        internal static Button CreateButton(Transform parent, string text, Action onClick)
        {
            RectTransform rect = NewRect("Button", parent);

            Image image = rect.gameObject.AddComponent<Image>();
            image.color = ButtonNormal;

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colours = button.colors;
            colours.normalColor = ButtonNormal;
            colours.highlightedColor = ButtonHover;
            colours.pressedColor = ButtonPressed;
            colours.selectedColor = ButtonNormal;
            button.colors = colours;

            if (onClick != null)
            {
                button.onClick.AddListener(() => onClick());
            }

            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = RowHeight;
            element.minHeight = RowHeight;

            RectTransform textRect = NewRect("Text", rect);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 0f);
            textRect.offsetMax = new Vector2(-16f, 0f);

            var label = textRect.gameObject.AddComponent<TextMeshProUGUI>();
            if (_font != null)
            {
                label.font = _font;
            }

            label.text = text;
            label.fontSize = 20f;
            label.color = BodyColour;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;

            return button;
        }

        /// <summary>A labelled row of buttons, for related one-shot actions.</summary>
        internal static void CreateButtonRow(
            Transform parent, params (string Label, Action OnClick)[] actions)
        {
            RectTransform row = NewRect("Row", parent);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            var element = row.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = RowHeight;
            element.minHeight = RowHeight;

            foreach ((string label, Action onClick) in actions)
            {
                Button button = CreateButton(row, label, onClick);
                // The row controls sizing; drop the per-button height preference.
                UnityEngine.Object.Destroy(button.GetComponent<LayoutElement>());
                foreach (TMP_Text text in button.GetComponentsInChildren<TMP_Text>())
                {
                    text.alignment = TextAlignmentOptions.Center;
                }
            }
        }
        /// <summary>
        /// Builds a labelled text field with an action button.
        ///
        /// A working TMP_InputField needs a text area, caret and viewport wired together,
        /// so rather than assembling one it clones a field already living in the game's
        /// UI. Returns false when none can be found, leaving the caller to skip the row.
        /// </summary>
        internal static bool CreateInputRow(
            Transform parent, string label, string placeholder,
            params (string Text, Action<string> OnClick)[] buttons)
        {
            TMP_InputField source = FindInputFieldTemplate();
            if (source == null)
            {
                return false;
            }

            CreateLabel(parent, label, InputFontSize, BodyColour, 30f);

            RectTransform row = NewRect("InputRow", parent);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            var rowElement = row.gameObject.AddComponent<LayoutElement>();
            rowElement.preferredHeight = InputRowHeight;
            rowElement.minHeight = InputRowHeight;

            var field = UnityEngine.Object.Instantiate(source, row);
            field.gameObject.name = "Input";
            field.gameObject.SetActive(true);
            field.onValueChanged.RemoveAllListeners();
            field.onSubmit.RemoveAllListeners();
            field.onEndEdit.RemoveAllListeners();
            field.text = string.Empty;
            field.characterLimit = 64;

            if (field.placeholder is TMP_Text hint)
            {
                hint.text = placeholder;
                hint.fontSize = InputFontSize;
                hint.enableAutoSizing = false;
            }

            var fieldElement = field.GetComponent<LayoutElement>() ??
                               field.gameObject.AddComponent<LayoutElement>();
            fieldElement.flexibleWidth = 3f;
            // The clone arrives with whatever height it had in its original panel.
            fieldElement.preferredHeight = InputRowHeight;
            fieldElement.minHeight = InputRowHeight;

            // The clone also keeps its original font size, which is far smaller than the
            // rest of this menu and leaves the typed value unreadable.
            if (field.textComponent != null)
            {
                field.textComponent.fontSize = InputFontSize;
                field.textComponent.enableAutoSizing = false;
            }

            field.pointSize = InputFontSize;

            foreach ((string caption, Action<string> action) in buttons)
            {
                Action<string> handler = action;
                Button button = CreateButton(row, caption, () => handler?.Invoke(field.text));
                UnityEngine.Object.Destroy(button.GetComponent<LayoutElement>());
                button.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                foreach (TMP_Text text in button.GetComponentsInChildren<TMP_Text>())
                {
                    text.alignment = TextAlignmentOptions.Center;
                }
            }

            return true;
        }

        /// <summary>
        /// A text field that offers live suggestions as you type, so an id like
        /// "ba:itemname_bread" can be found by typing "bread" instead of picking it out
        /// of a list of hundreds.
        ///
        /// <paramref name="search"/> returns (value, label) pairs; picking one puts its
        /// value in the field and hands it to <paramref name="onPick"/>.
        /// </summary>
        internal static bool CreateSearchRow(
            Transform parent, string label, string placeholder, string buttonText,
            Func<string, List<(string Value, string Label)>> search,
            Action<string> onSubmit,
            int maxSuggestions = 6)
        {
            TMP_InputField source = FindInputFieldTemplate();
            if (source == null)
            {
                return false;
            }

            CreateLabel(parent, label, InputFontSize, BodyColour, 30f);

            RectTransform row = NewRect("SearchRow", parent);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            var rowElement = row.gameObject.AddComponent<LayoutElement>();
            rowElement.preferredHeight = InputRowHeight;
            rowElement.minHeight = InputRowHeight;

            var field = UnityEngine.Object.Instantiate(source, row);
            field.gameObject.name = "Input";
            field.gameObject.SetActive(true);
            field.onValueChanged.RemoveAllListeners();
            field.onSubmit.RemoveAllListeners();
            field.onEndEdit.RemoveAllListeners();
            field.text = string.Empty;
            field.characterLimit = 64;

            if (field.placeholder is TMP_Text hint)
            {
                hint.text = placeholder;
                hint.fontSize = InputFontSize;
                hint.enableAutoSizing = false;
            }

            var fieldElement = field.GetComponent<LayoutElement>() ??
                               field.gameObject.AddComponent<LayoutElement>();
            fieldElement.flexibleWidth = 3f;
            // The clone arrives with whatever height it had in its original panel.
            fieldElement.preferredHeight = InputRowHeight;
            fieldElement.minHeight = InputRowHeight;

            // The clone also keeps its original font size, which is far smaller than the
            // rest of this menu and leaves the typed value unreadable.
            if (field.textComponent != null)
            {
                field.textComponent.fontSize = InputFontSize;
                field.textComponent.enableAutoSizing = false;
            }

            field.pointSize = InputFontSize;

            Button go = CreateButton(row, buttonText, () => onSubmit?.Invoke(field.text));
            UnityEngine.Object.Destroy(go.GetComponent<LayoutElement>());
            go.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            foreach (TMP_Text text in go.GetComponentsInChildren<TMP_Text>())
            {
                text.alignment = TextAlignmentOptions.Center;
            }

            // Suggestions live in their own column so they can grow and shrink without
            // disturbing the row above.
            RectTransform suggestions = NewRect("Suggestions", parent);
            var suggestionLayout = suggestions.gameObject.AddComponent<VerticalLayoutGroup>();
            suggestionLayout.spacing = 2f;
            suggestionLayout.childControlWidth = true;
            suggestionLayout.childControlHeight = false;
            suggestionLayout.childForceExpandWidth = true;
            suggestionLayout.childForceExpandHeight = false;
            suggestions.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            field.onValueChanged.AddListener(typed =>
            {
                for (int i = suggestions.childCount - 1; i >= 0; i--)
                {
                    UnityEngine.Object.Destroy(suggestions.GetChild(i).gameObject);
                }

                if (string.IsNullOrWhiteSpace(typed) || search == null)
                {
                    return;
                }

                foreach ((string value, string caption) in search(typed))
                {
                    string chosen = value;
                    Button option = CreateButton(suggestions, caption + "   (" + value + ")", () =>
                    {
                        // Fill the field so the choice is visible, then act on it.
                        field.SetTextWithoutNotify(chosen);
                        for (int i = suggestions.childCount - 1; i >= 0; i--)
                        {
                            UnityEngine.Object.Destroy(suggestions.GetChild(i).gameObject);
                        }

                        onSubmit?.Invoke(chosen);
                    });

                    LayoutElement element = option.GetComponent<LayoutElement>();
                    element.preferredHeight = RowHeight;
                    element.minHeight = RowHeight;
                    foreach (TMP_Text text in option.GetComponentsInChildren<TMP_Text>())
                    {
                        text.fontSize = 18f;
                    }
                }
            });

            return true;
        }

        private static TMP_InputField FindInputFieldTemplate()
        {
            // Includes inactive objects: most of the game's fields belong to panels that
            // are closed during normal play.
            foreach (TMP_InputField candidate in Resources.FindObjectsOfTypeAll<TMP_InputField>())
            {
                if (candidate != null && candidate.textComponent != null)
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
