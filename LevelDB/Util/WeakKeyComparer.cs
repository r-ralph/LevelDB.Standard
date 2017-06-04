﻿// Copyright 2004-2011 Castle Project - http://www.castleproject.org/
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;

namespace LevelDB.Util
{
    public class WeakKeyComparer<TKey> : IEqualityComparer<object> where TKey : class
    {
        public static readonly WeakKeyComparer<TKey> Default =
            new WeakKeyComparer<TKey>(EqualityComparer<TKey>.Default);

        private readonly IEqualityComparer<TKey> _comparer;

        public WeakKeyComparer(IEqualityComparer<TKey> comparer)
        {
            _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
        }

        public object Wrap(TKey key)
        {
            return new WeakKey(key, _comparer.GetHashCode(key));
        }

        public TKey Unwrap(object obj)
        {
            var weak = obj as WeakKey;
            return weak != null
                ? (TKey) weak.Target
                : (TKey) obj;
        }

        public int GetHashCode(object obj)
        {
            var weak = obj as WeakKey;
            return weak != null
                ? weak.GetHashCode()
                : _comparer.GetHashCode((TKey) obj);
        }

        public new bool Equals(object objA, object objB)
        {
            var keyA = Unwrap(objA);
            var keyB = Unwrap(objB);

            return keyA != null
                ? keyB != null && _comparer.Equals(keyA, keyB) // live object cannot equal a collected object
                : keyB == null && ReferenceEquals(objA, objB);
        }
    }
}