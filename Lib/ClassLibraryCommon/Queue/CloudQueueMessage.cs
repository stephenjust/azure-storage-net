// -----------------------------------------------------------------------------------------
// <copyright file="CloudQueueMessage.cs" company="Microsoft">
//    Copyright 2013 Microsoft Corporation
// 
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
// 
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
// </copyright>
// -----------------------------------------------------------------------------------------

namespace Microsoft.Azure.Storage.Queue
{
    using System;

    /// <summary>
    /// Represents a message in the Microsoft Azure Queue service.
    /// </summary>
    public sealed partial class CloudQueueMessage
    {
        /// <summary>
        /// Sets the content of this message.
        /// </summary>
        /// <param name="content">The content of the message as a byte array.</param>
        [Obsolete("Use SetMessageContent2(byte[])")]
        public void SetMessageContent(byte[] content)
        {
            this.SetMessageContent2(content);
        }
    }
}
