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

using System.Text;

namespace Hpdi.VssLogicalLib
{
    /// <summary>
    /// Factory for obtaining VssDatabase instances.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public class VssDatabaseFactory
    {
        private readonly string path;

        private Encoding encoding;
        public Encoding Encoding
        {
            get { return encoding; }
            set { encoding = value; }
        }

        public VssDatabaseFactory(string path)
        {
            this.path = path;
            this.encoding = GetSystemDefaultEncoding();
        }

        public VssDatabase Open()
        {
            return new VssDatabase(path, encoding);
        }

        // In .NET Framework, Encoding.Default returned the system code page.
        // In .NET 8, it returns UTF-8, breaking VSS filename reading.
        private static Encoding GetSystemDefaultEncoding()
        {
            int ansiCodePage = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ANSICodePage;
            return Encoding.GetEncoding(ansiCodePage);
        }
    }
}
