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
using System.Windows.Forms;

namespace Hpdi.Vss2Git
{
    /// <summary>
    /// MessageBox-based implementation of user interaction
    /// </summary>
    public class MessageBoxUserInteraction : IUserInteraction
    {
        private readonly IWin32Window owner;

        public MessageBoxUserInteraction(IWin32Window owner = null)
        {
            this.owner = owner;
        }

        public ErrorAction ReportError(string message, ErrorActionOptions options)
        {
            var buttons = options == ErrorActionOptions.RetryCancel
                ? MessageBoxButtons.RetryCancel
                : MessageBoxButtons.AbortRetryIgnore;

            var result = MessageBox.Show(owner, message, "Error", buttons, MessageBoxIcon.Error);

            return result switch
            {
                DialogResult.Retry => ErrorAction.Retry,
                DialogResult.Ignore => ErrorAction.Ignore,
                _ => ErrorAction.Abort
            };
        }

        public bool Confirm(string message, string title)
        {
            var result = MessageBox.Show(owner, message, title,
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            return result == DialogResult.Yes;
        }

        public void ShowFatalError(string message, Exception exception)
        {
            var fullMessage = exception != null
                ? $"{message}\n\n{ExceptionFormatter.Format(exception)}"
                : message;

            MessageBox.Show(owner, fullMessage, "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
