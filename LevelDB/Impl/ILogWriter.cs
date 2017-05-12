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

using System.IO;
using LevelDB.Util;

namespace LevelDB.Impl
{
    public interface ILogWriter
    {
        bool IsClosed { get; }

        void Close();

        void Delete();

        FileInfo File { get; }

        long FileNumber { get; }

        // Writes a stream of chunks such that no chunk is split across a block boundary
        void AddRecord(Slice record, bool force);
    }
}