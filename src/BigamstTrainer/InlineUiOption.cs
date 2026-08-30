using System;
using BigAmbitions.Mods;
using UnityEngine;

namespace BigamstTrainer
{
    /// <summary>
    /// A placeholder in the shared option list that the phone panel replaces with its own
    /// controls, so text fields can sit inside the right section instead of being appended
    /// at the end.
    ///
    /// The Options menu has no text input, so this deliberately renders nothing there —
    /// overriding SpawnUi to do nothing also suppresses the base class's "no renderer"
    /// error.
    /// </summary>
    internal sealed class InlineUiOption : ModOption
    {
        internal InlineUiOption(Action<Transform> buildForPhone)
            : base(null, string.Empty)
        {
            BuildForPhone = buildForPhone;
        }

        internal Action<Transform> BuildForPhone { get; }

        public override void SpawnUi(Transform parent, string modId)
        {
            // Nothing to draw in the Options menu.
        }
    }
}
