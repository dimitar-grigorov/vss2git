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

namespace Hpdi.Vss2Git
{
    /// <summary>
    /// Abstraction for user interaction (errors, confirmations)
    /// </summary>
    public interface IUserInteraction
    {
        /// <summary>
        /// Report an error and get user decision
        /// </summary>
        ErrorAction ReportError(string message, ErrorActionOptions options);

        /// <summary>
        /// Ask for confirmation before proceeding
        /// </summary>
        bool Confirm(string message, string title);

        /// <summary>
        /// Display a fatal error (no recovery)
        /// </summary>
        void ShowFatalError(string message, Exception exception);
    }

    public enum ErrorAction
    {
        Abort,
        Retry,
        Ignore
    }

    [Flags]
    public enum ErrorActionOptions
    {
        AbortRetryIgnore = 0,
        RetryCancel = 1
    }
}
