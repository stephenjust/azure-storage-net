﻿// -----------------------------------------------------------------------------------------
// <copyright file="FileClientTests.cs" company="Microsoft">
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Azure.Storage.Shared.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.Storage.File.Protocol
{
    internal class FileClientTests
    {
        public FileContext FileContext { get; private set; }

        public FileProperties Properties { get; private set; }
        public string FileName { get; private set; }
        public string ShareName { get; private set; }
        public string PublicFileName { get; private set; }
        public string PublicShareName { get; private set; }
        public byte[] Content { get; private set; }

        private Random random = new Random();

        public FileClientTests(bool owner, bool async, int timeout)
        {
            FileContext = new FileContext(owner, async, timeout);
        }

        public async Task Initialize()
        {
            ShareName = "defaultshare14";
            PublicShareName = "publicshare14";
            FileName = "defaultfile14";
            PublicFileName = "public14";

            Content = new byte[7000];
            random.NextBytes(Content);

            await CreateShare(ShareName);
            await CreateFile(ShareName, FileName, false);
            await CreateShare(PublicShareName);
            await CreateFile(PublicShareName, PublicFileName, true);
        }

        public async Task Cleanup()
        {
            await DeleteShare(ShareName);
            await DeleteShare(PublicShareName);
        }

        public async Task CreateShare(string shareName)
        {
            await CreateShare(shareName, 3);
        }

        public async Task CreateShare(string shareName, int retries)
        {
            // by default, sleep 35 seconds between retries
            await CreateShare(shareName, retries, 35000);
        }

        public async Task CreateShare(string shareName, int retries, int millisecondsBetweenRetries)
        {
            while (true)
            {
                HttpRequestMessage request = FileTests.CreateShareRequest(FileContext, shareName);
                Assert.IsTrue(request != null, "Failed to create HttpRequestMessage");

                HttpResponseMessage response = await FileTestUtils.GetResponse(request, FileContext);
                HttpStatusCode statusCode = response.StatusCode;
                string statusDescription = response.ReasonPhrase;
                StorageExtendedErrorInformation error = await StorageExtendedErrorInformation.ReadFromStreamAsync(await response.Content.ReadAsStreamAsync());
                response.Dispose();

                // if the share is being deleted, retry up to the specified times.
                if (statusCode == HttpStatusCode.Conflict && error != null && error.ErrorCode == FileErrorCodeStrings.ShareBeingDeleted && retries > 0)
                {
                    Thread.Sleep(millisecondsBetweenRetries);
                    retries--;
                    continue;
                }

                Assert.AreNotEqual(HttpStatusCode.NotFound, statusCode, "Failed to create share");

                break;
            }
        }

        public async Task DeleteShare(string shareName)
        {
            HttpRequestMessage request = FileTests.DeleteShareRequest(FileContext, shareName, null);
            Assert.IsTrue(request != null, "Failed to create HttpRequestMessage");
            HttpResponseMessage response = await FileTestUtils.GetResponse(request, FileContext);
            HttpStatusCode statusCode = response.StatusCode;
            string statusDescription = response.ReasonPhrase;
            response.Dispose();
        }

        public async Task CreateFile(string shareName, string fileName, bool isPublic)
        {
            Properties = new FileProperties();
            HttpRequestMessage request = FileTests.PutFileRequest(FileContext, shareName, fileName, Properties, Content, 7000, null);
            Assert.IsTrue(request != null, "Failed to create HttpRequestMessage");

            CancellationTokenSource timeout = new CancellationTokenSource(30000);

            HttpResponseMessage response = await FileTestUtils.GetResponse(request, FileContext, timeout);
            HttpStatusCode statusCode = response.StatusCode;
            string statusDescription = response.ReasonPhrase;
            response.Dispose();
            if (statusCode != HttpStatusCode.Created)
            {
                Assert.Fail(string.Format("Failed to create file: {0}, Status: {1}, Status Description: {2}", shareName, statusCode, statusDescription));
            }
        }

        public async Task WriteRange(string fileName, string shareName, byte[] content, HttpStatusCode? expectedError)
        {
            FileRange range = new FileRange(0, content.Length-1);
            HttpRequestMessage request = FileTests.WriteRangeRequest(FileContext, shareName, fileName, range, content.Length, null);
            Assert.IsTrue(request != null, "Failed to create HttpRequestMessage");
            
            request.Content = new ByteArrayContent(content);
            HttpResponseMessage response = await FileTestUtils.GetResponse(request, FileContext);
            try
            {
                FileTests.WriteRangeResponse(response, FileContext, expectedError);
            }
            finally
            {
                response.Dispose();
            }

        }
        public async Task DeleteFile(string shareName, string fileName)
        {
            HttpRequestMessage request = FileTests.DeleteFileRequest(FileContext, shareName, fileName, null);
            Assert.IsTrue(request != null, "Failed to create HttpRequestMessage");
            HttpResponseMessage response = await FileTestUtils.GetResponse(request, FileContext);
            response.Dispose();
        }

        public async Task PutFileScenarioTest(string shareName, string fileName, FileProperties properties, byte[] content, HttpStatusCode? expectedError)
        {
            HttpRequestMessage request = FileTests.PutFileRequest(FileContext, shareName, fileName, properties, content, content.Length, null);
            request.Content = new ByteArrayContent(content);
            Assert.IsTrue(request != null, "Failed to create HttpRequestMessage");
            //HttpRequestHandler.SetContentLength(request, content.Length);

            HttpResponseMessage response = await FileTestUtils.GetResponse(request, FileContext);
            try
            {
                FileTests.PutFileResponse(response, FileContext, expectedError);
            }
            finally
            {
                response.Dispose();
            }
        }

        public async Task ClearRangeScenarioTest(string shareName, string fileName, HttpStatusCode? expectedError)
        {
            // 1. Create Sparse File
            int fileSize = 128 * 1024;

            FileProperties properties = new FileProperties();
            Uri uri = FileTests.ConstructPutUri(FileContext.Address, shareName, fileName);
            OperationContext opContext = new OperationContext();
            HttpRequestMessage webRequest = FileHttpRequestMessageFactory.Create(uri, FileContext.Timeout, properties, fileSize, null, null, opContext, null, null);

            using (HttpResponseMessage response = await FileTestUtils.GetResponse(webRequest, FileContext))
            {
                FileTests.PutFileResponse(response, FileContext, expectedError);
            }

            // 2. Now upload some ranges
            for (int m = 0; m * 512 * 4 < fileSize; m++)
            {
                int startOffset = 512 * 4 * m;
                int length = 512;

                FileRange range = new FileRange(startOffset, startOffset + length - 1);
                opContext = new OperationContext();
                HttpRequestMessage rangeRequest = FileHttpRequestMessageFactory.PutRange(uri, FileContext.Timeout, range, FileRangeWrite.Update, null, null, opContext, null, null);
                HttpRequestHandler.SetContentLength(rangeRequest, 512);

                byte[] content = new byte[length];
                random.NextBytes(content);

                rangeRequest.Content = new ByteArrayContent(content);

                using (HttpResponseMessage rangeResponse = await FileTestUtils.GetResponse(rangeRequest, FileContext))
                {
                }
            }

            // 3. Now do a List Ranges
            List<FileRange> fileRanges = new List<FileRange>();
            opContext = new OperationContext();
            HttpRequestMessage listRangesRequest = FileHttpRequestMessageFactory.ListRanges(uri, FileContext.Timeout, null, null, null, null, null, opContext, null, null);
            using (HttpResponseMessage rangeResponse = await FileTestUtils.GetResponse(listRangesRequest, FileContext))
            {
                IEnumerable<FileRange> ranges = await ListRangesResponse.ParseAsync(await rangeResponse.Content.ReadAsStreamAsync(), CancellationToken.None);
                fileRanges.AddRange(ranges);
            }

            // 4. Now Clear some ranges
            bool skipFlag = false;
            foreach (FileRange pRange in fileRanges)
            {
                skipFlag = !skipFlag;
                if (skipFlag)
                {
                    continue;
                }

                opContext = new OperationContext();
                HttpRequestMessage clearRangeRequest = FileHttpRequestMessageFactory.PutRange(uri, FileContext.Timeout, pRange, FileRangeWrite.Clear, null, null, opContext, null, null);
                HttpRequestHandler.SetContentLength(clearRangeRequest, 0);
                using (HttpResponseMessage clearResponse = await FileTestUtils.GetResponse(clearRangeRequest, FileContext))
                {
                }
            }

            // 5. Get New ranges and verify
            List<FileRange> newFileRanges = new List<FileRange>();

            opContext = new OperationContext();
            HttpRequestMessage newFileRangeRequest = FileHttpRequestMessageFactory.ListRanges(uri, FileContext.Timeout, null, null, null, null, null, opContext, null, null);
            using (HttpResponseMessage newFileRangeResponse =   await FileTestUtils.GetResponse(newFileRangeRequest, FileContext))
            {
                IEnumerable<FileRange> ranges = await ListRangesResponse.ParseAsync(await newFileRangeResponse.Content.ReadAsStreamAsync(), CancellationToken.None);
                newFileRanges.AddRange(ranges);
            }

            Assert.AreEqual(fileRanges.Count(), newFileRanges.Count() * 2);
            for (int l = 0; l < newFileRanges.Count(); l++)
            {
                Assert.AreEqual(fileRanges[2 * l].StartOffset, newFileRanges[l].StartOffset);
                Assert.AreEqual(fileRanges[2 * l].EndOffset, newFileRanges[l].EndOffset);
            }
        }

        public async Task GetFileScenarioTest(string shareName, string fileName, FileProperties properties,
            HttpStatusCode? expectedError)
        {
            HttpRequestMessage request = FileTests.GetFileRequest(FileContext, shareName, fileName, null);
            Assert.IsTrue(request != null, "Failed to create HttpRequestMessage");
            HttpResponseMessage response = await FileTestUtils.GetResponse(request, FileContext);
            try
            {
                FileTests.GetFileResponse(response, FileContext, properties, expectedError);
            }
            finally
            {
                response.Dispose();
            }
        }

        /// <summary>
        /// Sends a get file range request with the given parameters and validates both request and response.
        /// </summary>
        /// <param name="shareName">The file's share's name.</param>
        /// <param name="fileName">The file's name.</param>
        /// <param name="leaseId">The lease ID, or null if there is no lease.</param>
        /// <param name="content">The total contents of the file.</param>
        /// <param name="offset">The offset of the contents we will get.</param>
        /// <param name="count">The number of bytes we will get, or null to get the rest of the file.</param>
        /// <param name="expectedError">The error code we expect from this operation, or null if we expect it to succeed.</param>
        public async Task GetFileRangeScenarioTest(string shareName, string fileName, byte[] content, long offset, long? count, HttpStatusCode? expectedError)
        {
            HttpRequestMessage request = FileTests.GetFileRangeRequest(FileContext, shareName, fileName, offset, count, null);
            Assert.IsTrue(request != null, "Failed to create HttpRequestMessage");

            HttpResponseMessage response = await FileTestUtils.GetResponse(request, FileContext);

            try
            {
                long endRange = count.HasValue ? count.Value + offset - 1 : content.Length - 1;
                byte[] selectedContent = null;

                // Compute expected content only if call is expected to succeed.
                if (expectedError == null)
                {
                    selectedContent = new byte[endRange - offset + 1];
                    Array.Copy(content, offset, selectedContent, 0, selectedContent.Length);
                }

                FileTests.CheckFileRangeResponse(response, FileContext, selectedContent, offset, endRange, content.Length, expectedError);
            }
            finally
            {
                response.Dispose();
            }
        }

        public async Task ListFilesAndDirectoriesScenarioTest(string shareName, FileListingContext listingContext, HttpStatusCode? expectedError, params string[] expectedFiles)
        {
            HttpRequestMessage request = FileTests.ListFilesAndDirectoriesRequest(FileContext, shareName, listingContext);
            Assert.IsTrue(request != null, "Failed to create HttpRequestMessage");
            HttpResponseMessage response = await FileTestUtils.GetResponse(request, FileContext);
            try
            {
                FileTests.ListFilesAndDirectoriesResponse(response, FileContext, expectedError);
                ListFilesAndDirectoriesResponse listFilesResponse = await ListFilesAndDirectoriesResponse.ParseAsync(await response.Content.ReadAsStreamAsync(), CancellationToken.None);
                int i = 0;
                foreach (IListFileEntry item in listFilesResponse.Files)
                {
                    ListFileEntry file = item as ListFileEntry;
                    if (expectedFiles == null)
                    {
                        Assert.Fail("Should not have files.");
                    }
                    Assert.IsTrue(i < expectedFiles.Length, "Unexpected file: " + file.Name);
                    Assert.AreEqual<string>(expectedFiles[i++], file.Name, "Incorrect file.");
                }
                if (expectedFiles != null && i < expectedFiles.Length)
                {
                    Assert.Fail("Missing file: " + expectedFiles[i] + "(and " + (expectedFiles.Length - i - 1) + " more).");
                }
            }
            finally
            {
                response.Dispose();
            }
        }

        public async Task ListSharesScenarioTest(ListingContext listingContext, HttpStatusCode? expectedError, params string[] expectedShares)
        {
            HttpRequestMessage request = FileTests.ListSharesRequest(FileContext, listingContext);
            Assert.IsTrue(request != null, "Failed to create HttpRequestMessage");

            HttpResponseMessage response = await FileTestUtils.GetResponse(request, FileContext);
            try
            {
                FileTests.ListSharesResponse(response, FileContext, expectedError);
                ListSharesResponse listSharesResponse = await ListSharesResponse.ParseAsync(await response.Content.ReadAsStreamAsync(), CancellationToken.None);
                int i = 0;
                foreach (FileShareEntry item in listSharesResponse.Shares)
                {
                    if (expectedShares == null)
                    {
                        Assert.Fail("Should not have shares.");
                    }
                    Assert.IsTrue(i < expectedShares.Length, "Unexpected share: " + item.Name);
                    Assert.AreEqual<string>(expectedShares[i++], item.Name, "Incorrect share.");
                }
                if (expectedShares != null && i < expectedShares.Length)
                {
                    Assert.Fail("Missing share: " + expectedShares[i] + "(and " + (expectedShares.Length - i - 1) + " more).");
                }
            }
            finally
            {
                response.Dispose();
            }
        }

        public static Uri ConstructUri(string address, params string[] folders)
        {
            Assert.IsNotNull(address);

            string uriString = address;
            foreach (string folder in folders)
            {
                uriString = String.Format("{0}/{1}", uriString, folder);
            }
            Uri uri = null;
            uri = new Uri(uriString);
            return uri;
        }
    }
}

