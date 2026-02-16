/* Copyright 2009 HPDI, LLC
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace Hpdi.Vss2Git
{
    /// <summary>
    /// Interface for Git repository operations.
    /// </summary>
    interface IGitRepository : IDisposable
    {
        TimeSpan ElapsedTime { get; }

        Encoding CommitEncoding { get; set; }

        void Init();

        void SetConfig(string name, string value);

        bool Add(string path);

        bool Add(IEnumerable<string> paths);

        bool AddAll();

        bool AddAll(IEnumerable<string> changedPaths);

        void Remove(string path, bool recursive);

        void Move(string sourcePath, string destPath);

        bool Commit(string authorName, string authorEmail, string comment, DateTime localTime);

        void Tag(string name, string taggerName, string taggerEmail, string comment, DateTime localTime);

        void Compact();

        /// <summary>
        /// Finalizes the repository after migration by creating the git index from HEAD.
        /// Required for FastImport/LibGit2Sharp backends which bypass the index for performance.
        /// </summary>
        /// <returns>List of files in HEAD that are missing from working tree (if any)</returns>
        IList<string> FinalizeRepository();
    }
}
