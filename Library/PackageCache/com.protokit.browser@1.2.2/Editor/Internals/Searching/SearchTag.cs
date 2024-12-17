// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using System;

namespace Protokit.Browser {

    public struct SearchTag {

        public readonly string Name;
        public readonly Action Remove;

        public SearchTag(string name, Action onRemove) {
            Name = name;
            Remove = onRemove;
        }
    }
}
