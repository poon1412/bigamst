using System;
using System.Collections.Generic;
using BAModAPI;
using UnityEngine;
using UnityEngine.UI;

namespace BigamstTrainer
{
    /// <summary>
    /// Picks icons and colours for the buttons this mod adds to the phone.
    ///
    /// A cloned app button keeps the icon and tint of whatever app it was copied from,
    /// which reads as a duplicate of that app rather than something new. There is no
    /// asset bundle here to ship artwork in, so the icons come from sprites the game has
    /// already loaded, matched by name.
    /// </summary>
    internal static class PhoneIcons
    {
        /// <summary>
        /// Amber, sitting alongside the game's own tile colours rather than fighting them.
        /// A saturated red drew the eye far harder than an app icon should.
        /// </summary>
        internal static readonly Color TrainerTint = new Color(0.96f, 0.75f, 0.19f, 1f);

        /// <summary>Deliberately muted: the page control is navigation, not a feature.</summary>
        internal static readonly Color NavTint = new Color(0.36f, 0.40f, 0.46f, 1f);

        private static Sprite[] _sprites;

        internal static void Forget()
        {
            _sprites = null;
            _roundedSquare = null;
        }

        /// <summary>
        /// Every loaded sprite, fetched once. FindObjectsOfTypeAll is expensive and
        /// returns thousands of entries, so this must not be called per frame.
        /// </summary>
        private static Sprite[] AllSprites =>
            _sprites ?? (_sprites = Resources.FindObjectsOfTypeAll<Sprite>());

        /// <summary>
        /// First sprite whose name contains one of <paramref name="keywords"/>, in the
        /// order given, so earlier keywords win.
        /// </summary>
        internal static Sprite Find(params string[] keywords)
        {
            try
            {
                foreach (string keyword in keywords)
                {
                    foreach (Sprite sprite in AllSprites)
                    {
                        if (sprite != null && !string.IsNullOrEmpty(sprite.name) &&
                            sprite.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return sprite;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // An icon is cosmetic; the cloned one still shows.
            }

            return null;
        }

        /// <summary>
        /// Applies an icon and tint to a cloned app button.
        ///
        /// The tile is built from a coloured background image plus a white glyph. The
        /// glyph is the smaller of the two, so size tells them apart without needing the
        /// private fields AppButton keeps them in.
        /// </summary>
        /// <summary>
        /// Gives a cloned tile its own look: a coloured rounded square with one of the
        /// game's glyphs on top.
        ///
        /// The game's app tiles are single sprites — "AppIcon-&lt;App&gt;" bakes the
        /// coloured square and its white glyph together — and only eight exist, one per
        /// app, so there is nothing spare to borrow. Tinting one is no good either: it
        /// recolours the glyph along with the square. The game does ship 111 plain
        /// "Icon-*" glyphs though, so the square is generated here and a real glyph laid
        /// over it.
        /// </summary>
        internal static void BuildTile(GameObject button, Color tint, string glyphName)
        {
            try
            {
                Image tile = null;
                foreach (Image image in button.GetComponentsInChildren<Image>(includeInactive: true))
                {
                    // The tile art is the only image showing an AppIcon sprite; the others
                    // are the transparent cell and the notification badge.
                    if (image?.sprite != null &&
                        image.sprite.name.StartsWith("AppIcon", StringComparison.OrdinalIgnoreCase))
                    {
                        tile = image;
                        break;
                    }
                }

                if (tile == null)
                {
                    return;
                }

                tile.sprite = RoundedSquare;
                tile.color = tint;

                Sprite glyph = Find(glyphName);
                if (glyph == null)
                {
                    return;
                }

                // A child rather than a second sprite on the same image, so the square can
                // be coloured while the glyph stays white.
                var go = new GameObject("Glyph", typeof(RectTransform));
                var rect = (RectTransform)go.transform;
                rect.SetParent(tile.transform, worldPositionStays: false);
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;

                float size = tile.rectTransform.rect.width * 0.52f;
                rect.sizeDelta = new Vector2(size, size);

                Image image2 = go.AddComponent<Image>();
                image2.sprite = glyph;
                image2.color = Color.white;
                image2.raycastTarget = false;
                image2.preserveAspect = true;
            }
            catch (Exception)
            {
                // Leave the cloned artwork in place.
            }
        }

        private static Sprite _roundedSquare;

        /// <summary>
        /// A white rounded square, drawn once. White so it can be tinted to any colour.
        /// </summary>
        private static Sprite RoundedSquare
        {
            get
            {
                if (_roundedSquare != null)
                {
                    return _roundedSquare;
                }

                const int size = 128;
                const float radius = 26f;

                // The game's AppIcon sprites carry transparent padding, so the visible
                // square is smaller than the image rect. Filling the rect edge to edge
                // makes this tile read as larger than its neighbours; inset to match.
                const float margin = 7f;

                var texture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                };

                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        // Distance past the rounded corner, so edges come out smooth
                        // rather than stepped.
                        float dx = Mathf.Max(margin + radius - x, 0f,
                                             x - (size - 1 - margin - radius));
                        float dy = Mathf.Max(margin + radius - y, 0f,
                                             y - (size - 1 - margin - radius));
                        float distance = Mathf.Sqrt(dx * dx + dy * dy);
                        float alpha = Mathf.Clamp01(radius - distance + 0.5f);
                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply();

                _roundedSquare = Sprite.Create(
                    texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
                return _roundedSquare;
            }
        }

        /// <summary>
        /// Logs every Image inside a tile with its path, size and colour, so the coloured
        /// square can be identified from fact rather than guessed at by size.
        /// </summary>
        internal static void DumpStructure(GameObject button, IModLogger log)
        {
            if (log == null || button == null)
            {
                return;
            }

            try
            {
                var lines = new List<string>();
                foreach (Image image in button.GetComponentsInChildren<Image>(includeInactive: true))
                {
                    if (image == null || image.rectTransform == null)
                    {
                        continue;
                    }

                    // Path relative to the tile, so nesting is visible.
                    var path = new List<string>();
                    Transform node = image.transform;
                    while (node != null && node != button.transform)
                    {
                        path.Insert(0, node.name);
                        node = node.parent;
                    }

                    Rect rect = image.rectTransform.rect;
                    Color c = image.color;
                    lines.Add($"{(path.Count == 0 ? "<root>" : string.Join("/", path.ToArray()))}" +
                              $" [{rect.width:0}x{rect.height:0}]" +
                              $" rgba({c.r:0.00},{c.g:0.00},{c.b:0.00},{c.a:0.00})" +
                              $" sprite={(image.sprite == null ? "none" : image.sprite.name)}");
                }

                log.Info($"Tile '{button.name}' images: {string.Join(" | ", lines.ToArray())}");
            }
            catch (Exception exception)
            {
                log.Warn($"Could not dump tile structure: {exception.Message}");
            }
        }

        /// <summary>
        /// Logs sprite names matching some likely keywords, so real icon names can be
        /// chosen from what the game actually has rather than guessed at.
        /// </summary>
        internal static void ReportCandidates(IModLogger log)
        {
            if (log == null)
            {
                return;
            }

            // The whole tile is one sprite (AppIcon-*), so the useful question is which
            // of those exist — not which loose glyphs do.
            string[] keywords = { "AppIcon" };

            try
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var found = new List<string>();

                foreach (Sprite sprite in AllSprites)
                {
                    string name = sprite?.name;
                    if (string.IsNullOrEmpty(name) || !seen.Add(name))
                    {
                        continue;
                    }

                    foreach (string keyword in keywords)
                    {
                        if (name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            found.Add(name);
                            break;
                        }
                    }
                }

                found.Sort(StringComparer.OrdinalIgnoreCase);
                log.Info($"App icon sprites ({found.Count}): " +
                         string.Join(", ", found.ToArray(), 0, Math.Min(found.Count, 80)));
            }
            catch (Exception exception)
            {
                log.Warn($"Could not list icon candidates: {exception.Message}");
            }
        }
    }
}
