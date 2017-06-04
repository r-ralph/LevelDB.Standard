﻿#region Copyright

// Copyright 2017 Ralph (Tamaki Hidetsugu)
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

#endregion

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LevelDB.Guava;
using LevelDB.Util;

namespace LevelDB.Impl
{
    public class Level : ISeekingIterable<InternalKey, Slice>
    {
        public int LevelNumber { get; }
        public List<FileMetaData> Files { get; }
        private readonly TableCache _tableCache;
        private readonly InternalKeyComparator _internalKeyComparator;

        public Level(int levelNumber, List<FileMetaData> files, TableCache tableCache,
            InternalKeyComparator internalKeyComparator)
        {
            Preconditions.CheckArgument(levelNumber >= 0, $"{nameof(levelNumber)} is negative");
            Preconditions.CheckNotNull(files, $"{nameof(files)} is null");
            Preconditions.CheckNotNull(tableCache, $"{nameof(tableCache)} is null");
            Preconditions.CheckNotNull(internalKeyComparator, $"{nameof(internalKeyComparator)} is null");

            Files = new List<FileMetaData>(files);
            _tableCache = tableCache;
            _internalKeyComparator = internalKeyComparator;
            Preconditions.CheckArgument(levelNumber >= 0, $"{nameof(levelNumber)} is negative");
            LevelNumber = levelNumber;
        }

        public LevelIterator GetLevelIterator()
        {
            return CreateLevelConcatIterator(_tableCache, Files, _internalKeyComparator);
        }

        public IEnumerator<Entry<InternalKey, Slice>> GetEnumerator()
        {
            return GetLevelIterator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetLevelIterator();
        }

        public static LevelIterator CreateLevelConcatIterator(TableCache tableCache, IList<FileMetaData> files,
            InternalKeyComparator internalKeyComparator)
        {
            return new LevelIterator(tableCache, files, internalKeyComparator);
        }

        public LookupResult Get(LookupKey key, ReadStats readStats)
        {
            if (Files.Count == 0)
            {
                return null;
            }

            var fileMetaDataList = new List<FileMetaData>(Files.Count);
            if (LevelNumber == 0)
            {
                fileMetaDataList.AddRange(Files.Where(fileMetaData =>
                    _internalKeyComparator.UserComparator.Compare(key.UserKey, fileMetaData.Smallest.UserKey) >= 0 &&
                    _internalKeyComparator.UserComparator.Compare(key.UserKey, fileMetaData.Largest.UserKey) <= 0));
            }
            else
            {
                // Binary search to find earliest index whose largest key >= ikey.
                var index = CeilingEntryIndex(Files.Select(FileMetaData.GetLargestUserKey).ToList(), key.InternalKey,
                    _internalKeyComparator);

                // did we find any files that could contain the key?
                if (index >= Files.Count)
                {
                    return null;
                }

                // check if the smallest user key in the file is less than the target user key
                var fileMetaData = Files[index];
                if (_internalKeyComparator.UserComparator.Compare(key.UserKey, fileMetaData.Smallest.UserKey) < 0)
                {
                    return null;
                }

                // search this file
                fileMetaDataList.Add(fileMetaData);
            }

            FileMetaData lastFileRead = null;
            var lastFileReadLevel = -1;
            readStats.Clear();
            foreach (var fileMetaData in fileMetaDataList)
            {
                if (lastFileRead != null && readStats.SeekFile == null)
                {
                    // We have had more than one seek for this read.  Charge the first file.
                    readStats.SeekFile = lastFileRead;
                    readStats.SeekFileLevel = lastFileReadLevel;
                }

                lastFileRead = fileMetaData;
                lastFileReadLevel = LevelNumber;

                // open the iterator
                var iterator = _tableCache.NewIterator(fileMetaData);

                // seek to the key
                iterator.Seek(key.InternalKey);

                if (iterator.HasNext())
                {
                    // parse the key in the block
                    var entry = iterator.Next();
                    var internalKey = entry.Key;
                    Preconditions.CheckState(internalKey != null,
                        $"Corrupt key for {key.UserKey.ToString(Encoding.UTF8)}");

                    // if this is a value key (not a delete) and the keys match, return the value
                    // ReSharper disable once PossibleNullReferenceException
                    if (key.UserKey.Equals(internalKey.UserKey))
                    {
                        if (internalKey.ValueType == ValueType.Deletion)
                        {
                            return LookupResult.Deleted(key);
                        }
                        if (internalKey.ValueType == ValueType.Value)
                        {
                            return LookupResult.Ok(key, entry.Value);
                        }
                    }
                }
            }

            return null;
        }

        private static int CeilingEntryIndex<T>(List<T> list, T key, IComparer<T> comparator)
        {
            var insertionPoint = list.BinarySearch(key, comparator);
            if (insertionPoint < 0)
            {
                insertionPoint = -(insertionPoint + 1);
            }
            return insertionPoint;
        }

        public bool SomeFileOverlapsRange(Slice smallestUserKey, Slice largestUserKey)
        {
            var smallestInternalKey =
                new InternalKey(smallestUserKey, SequenceNumber.MaxSequenceNumber, ValueType.Value);
            var index = FindFile(smallestInternalKey);

            var userComparator = _internalKeyComparator.UserComparator;
            return index < Files.Count && userComparator.Compare(largestUserKey, Files[index].Smallest.UserKey) >= 0;
        }

        private int FindFile(InternalKey targetKey)
        {
            if (Files.Count == 0)
            {
                return 0;
            }

            // todo replace with Collections.binarySearch
            var left = 0;
            var right = Files.Count - 1;

            // binary search restart positions to find the restart position immediately before the targetKey
            while (left < right)
            {
                var mid = (left + right) / 2;

                if (_internalKeyComparator.Compare(Files[mid].Largest, targetKey) < 0)
                {
                    // Key at "mid.largest" is < "target".  Therefore all
                    // files at or before "mid" are uninteresting.
                    left = mid + 1;
                }
                else
                {
                    // Key at "mid.largest" is >= "target".  Therefore all files
                    // after "mid" are uninteresting.
                    right = mid;
                }
            }
            return right;
        }

        public void AddFile(FileMetaData fileMetaData)
        {
            // todo remove mutation
            Files.Add(fileMetaData);
        }

        public override string ToString()
        {
            return $"Level(levelNumber={LevelNumber}, files={Files})";
        }
    }
}