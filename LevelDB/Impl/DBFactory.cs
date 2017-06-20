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

using System;
using System.IO;

namespace LevelDB.Impl
{
    public class DBFactory : IDBFactory<WriteBatchImpl>
    {
        public DB<IDBIterator<Entry<byte[], byte[]>>, WriteBatchImpl> Open(DirectoryInfo path, Options options)
        {
            return new DbImpl(options, path);
        }

        public void Destroy(DirectoryInfo path, Options options)
        {
            path.Delete(true);
        }

        public void Repair(DirectoryInfo path, Options options)
        {
            throw new NotSupportedException();
        }
    }
}