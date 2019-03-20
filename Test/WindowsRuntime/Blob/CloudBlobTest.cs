﻿// -----------------------------------------------------------------------------------------
// <copyright file="CloudBlobTest.cs" company="Microsoft">
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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Azure.Storage.Blob
{
    [TestClass]
    public class CloudBlobTest : BlobTestBase
    {
        const string BlobName = "blob1";

        internal static async Task CreateForTestAsync(CloudBlockBlob blob, int blockCount, int blockSize, bool commit = true)
        {
            byte[] buffer = GetRandomBuffer(blockSize);
            List<string> blocks = GetBlockIdList(blockCount);

            foreach (string block in blocks)
            {
                using (MemoryStream stream = new MemoryStream(buffer))
                {
                    await blob.PutBlockAsync(block, stream, null);
                }
            }

            if (commit)
            {
                await blob.PutBlockListAsync(blocks);
            }
        }

        // Use TestInitialize to run code before running each test 
        [TestInitialize()]
        public void MyTestInitialize()
        {
            if (TestBase.BlobBufferManager != null)
            {
                TestBase.BlobBufferManager.OutstandingBufferCount = 0;
            }
        }
        //
        // Use TestCleanup to run code after each test has run
        [TestCleanup()]
        public void MyTestCleanup()
        {
            if (TestBase.BlobBufferManager != null)
            {
                Assert.AreEqual(0, TestBase.BlobBufferManager.OutstandingBufferCount);
            }
        }

       

        [TestMethod]
        [Description("Create snapshots of a blob")]
        [TestCategory(ComponentCategory.Blob)]
        [TestCategory(TestTypeCategory.UnitTest)]
        [TestCategory(SmokeTestCategory.NonSmoke)]
        [TestCategory(TenantTypeCategory.DevStore), TestCategory(TenantTypeCategory.DevFabric), TestCategory(TenantTypeCategory.Cloud)]
        public async Task CloudBlobSnapshotAsync()
        {
            CloudBlobContainer container = GetRandomContainerReference();
            try
            {
                await container.CreateAsync();

                MemoryStream originalData = new MemoryStream(GetRandomBuffer(1024));
                CloudBlockBlob blockBlob = container.GetBlockBlobReference(BlobName);
                await blockBlob.UploadFromStreamAsync(originalData);

                CloudBlob blob = container.GetBlobReference(BlobName);
                await blob.FetchAttributesAsync();
                Assert.IsFalse(blob.IsSnapshot);
                Assert.IsNull(blob.SnapshotTime, "Root blob has SnapshotTime set");
                Assert.IsFalse(blob.SnapshotQualifiedUri.Query.Contains("snapshot"));
                Assert.AreEqual(blob.Uri, blob.SnapshotQualifiedUri);

                CloudBlob snapshot1 = await blob.SnapshotAsync();
                Assert.AreEqual(blob.Properties.ETag, snapshot1.Properties.ETag);
                Assert.AreEqual(blob.Properties.LastModified, snapshot1.Properties.LastModified);
                Assert.IsTrue(snapshot1.IsSnapshot);
                Assert.IsNotNull(snapshot1.SnapshotTime, "Snapshot does not have SnapshotTime set");
                Assert.AreEqual(blob.Uri, snapshot1.Uri);
                Assert.AreNotEqual(blob.SnapshotQualifiedUri, snapshot1.SnapshotQualifiedUri);
                Assert.AreNotEqual(snapshot1.Uri, snapshot1.SnapshotQualifiedUri);
                Assert.IsTrue(snapshot1.SnapshotQualifiedUri.Query.Contains("snapshot"));

                CloudBlob snapshot2 = await blob.SnapshotAsync();
                Assert.IsTrue(snapshot2.SnapshotTime.Value > snapshot1.SnapshotTime.Value);

                await snapshot1.FetchAttributesAsync();
                await snapshot2.FetchAttributesAsync();
                await blob.FetchAttributesAsync();
                AssertAreEqual(snapshot1.Properties, blob.Properties);

                CloudBlob snapshot1Clone = new CloudBlob(new Uri(blob.Uri + "?snapshot=" + snapshot1.SnapshotTime.Value.ToString("O")), blob.ServiceClient.Credentials);
                Assert.IsNotNull(snapshot1Clone.SnapshotTime, "Snapshot clone does not have SnapshotTime set");
                Assert.AreEqual(snapshot1.SnapshotTime.Value, snapshot1Clone.SnapshotTime.Value);
                await snapshot1Clone.FetchAttributesAsync();
                AssertAreEqual(snapshot1.Properties, snapshot1Clone.Properties);

                // The query parser should not be case sensitive in detecting a snapshot in the query string
                CloudBlob snapshotParseVerifier = new CloudBlob(new Uri(blob.Uri + "?sNapshOt=" + snapshot1.SnapshotTime.Value.ToString("O")), blob.ServiceClient.Credentials);
                Assert.IsNotNull(snapshotParseVerifier.SnapshotTime, "Snapshot parse verifier did not successfully detect the snapshot time");
                Assert.AreEqual(snapshot1.SnapshotTime.Value, snapshotParseVerifier.SnapshotTime.Value);

                CloudBlob snapshotCopy = container.GetBlobReference("blob2");
                await snapshotCopy.StartCopyAsync(TestHelper.Defiddler(snapshot1.Uri));
                await WaitForCopyAsync(snapshotCopy);
                Assert.AreEqual(CopyStatus.Success, snapshotCopy.CopyState.Status);

                using (Stream snapshotStream = (await snapshot1.OpenReadAsync()))
                {
                    snapshotStream.Seek(0, SeekOrigin.End);
                    TestHelper.AssertStreamsAreEqual(originalData, snapshotStream);
                }

                await blockBlob.PutBlockListAsync(new List<string>());
                await blob.FetchAttributesAsync();

                using (Stream snapshotStream = (await snapshot1.OpenReadAsync()))
                {
                    snapshotStream.Seek(0, SeekOrigin.End);
                    TestHelper.AssertStreamsAreEqual(originalData, snapshotStream);
                }

                BlobResultSegment resultSegment = await container.ListBlobsSegmentedAsync(null, true, BlobListingDetails.All, null, null, null, null);
                List<IListBlobItem> blobs = resultSegment.Results.ToList();
                Assert.AreEqual(4, blobs.Count);
                AssertAreEqual(snapshot1, (CloudBlob)blobs[0]);
                AssertAreEqual(snapshot2, (CloudBlob)blobs[1]);
                AssertAreEqual(blob, (CloudBlob)blobs[2]);
                AssertAreEqual(snapshotCopy, (CloudBlob)blobs[3]);
            }
            finally
            {
                container.DeleteIfExistsAsync().Wait();
            }
        }

        [TestMethod]
        [Description("Create a snapshot with explicit metadata")]
        [TestCategory(ComponentCategory.Blob)]
        [TestCategory(TestTypeCategory.UnitTest)]
        [TestCategory(SmokeTestCategory.NonSmoke)]
        [TestCategory(TenantTypeCategory.DevStore), TestCategory(TenantTypeCategory.DevFabric), TestCategory(TenantTypeCategory.Cloud)]
        public async Task CloudBlobSnapshotMetadataAsync()
        {
            CloudBlobContainer container = GetRandomContainerReference();
            try
            {
                await container.CreateAsync();

                CloudBlockBlob blockBlob = container.GetBlockBlobReference(BlobName);
                await CloudBlockBlobTest.CreateForTestAsync(blockBlob, 2, 1024);

                CloudBlob blob = container.GetBlockBlobReference(BlobName);
                blob.Metadata["Hello"] = "World";
                blob.Metadata["Marco"] = "Polo";
                await blob.SetMetadataAsync();

                IDictionary<string, string> snapshotMetadata = new Dictionary<string, string>();
                snapshotMetadata["Hello"] = "Dolly";
                snapshotMetadata["Yoyo"] = "Ma";

                CloudBlob snapshot = await blob.SnapshotAsync(snapshotMetadata, null, null, null);

                // Test the client view against the expected metadata
                // None of the original metadata should be present
                Assert.AreEqual("Dolly", snapshot.Metadata["Hello"]);
                Assert.AreEqual("Ma", snapshot.Metadata["Yoyo"]);
                Assert.IsFalse(snapshot.Metadata.ContainsKey("Marco"));

                // Test the server view against the expected metadata
                await snapshot.FetchAttributesAsync();
                Assert.AreEqual("Dolly", snapshot.Metadata["Hello"]);
                Assert.AreEqual("Ma", snapshot.Metadata["Yoyo"]);
                Assert.IsFalse(snapshot.Metadata.ContainsKey("Marco"));
            }
            finally
            {
                container.DeleteIfExistsAsync().Wait();
            }
        }

        #region soft-delete
        #region TASK
        [TestMethod]
        [Description("Soft Delete and Undelete a blob with no snapshot TASK")]
        [TestCategory(ComponentCategory.Blob)]
        [TestCategory(TestTypeCategory.UnitTest)]
        [TestCategory(SmokeTestCategory.NonSmoke)]
        [TestCategory(TenantTypeCategory.DevStore), TestCategory(TenantTypeCategory.DevFabric), TestCategory(TenantTypeCategory.Cloud)]
        public void CloudBlobSoftDeleteNoSnapshotTask()
        {
            CloudBlobContainer container = GetRandomContainerReference();
            try
            {
                //Enables a delete retention policy on the blob with 1 day of default retention days
                container.ServiceClient.EnableSoftDelete();
                container.CreateAsync().Wait();

                // Upload some data to the blob.
                MemoryStream originalData = new MemoryStream(GetRandomBuffer(1024));
                CloudAppendBlob appendBlob = container.GetAppendBlobReference(BlobName);
                appendBlob.CreateOrReplaceAsync().Wait();
                appendBlob.AppendBlockAsync(originalData, null).Wait();


                CloudBlob blob = container.GetBlobReference(BlobName);
                Assert.IsTrue(blob.ExistsAsync().Result);
                Assert.IsFalse(blob.IsDeleted);

                blob.DeleteAsync().Wait();
                Assert.IsFalse(blob.ExistsAsync().Result);

                int blobCount = 0;
                BlobContinuationToken ct = null;
                do
                {
                    foreach (IListBlobItem item in container.ListBlobsSegmentedAsync
                        (null, true, BlobListingDetails.All, null, ct, null, null)
                        .Result
                        .Results
                        .ToList())
                    {
                        CloudAppendBlob blobItem = (CloudAppendBlob)item;
                        Assert.AreEqual(blobItem.Name, BlobName);
                        Assert.IsTrue(blobItem.IsDeleted);
                        Assert.IsNotNull(blobItem.Properties.DeletedTime);
                        Assert.AreEqual(blobItem.Properties.RemainingDaysBeforePermanentDelete, 0);
                        blobCount++;
                    }
                } while (ct != null);

                Assert.AreEqual(blobCount, 1);

                blob.UndeleteAsync().Wait();

                blob.FetchAttributesAsync().Wait();
                Assert.IsFalse(blob.IsDeleted);
                Assert.IsNull(blob.Properties.DeletedTime);
                Assert.IsNull(blob.Properties.RemainingDaysBeforePermanentDelete);

                blobCount = 0;
                ct = null;
                do
                {
                    foreach (IListBlobItem item in container.ListBlobsSegmentedAsync
                        (null, true, BlobListingDetails.All, null, ct, null, null)
                        .Result
                        .Results
                        .ToList())
                    {
                        CloudAppendBlob blobItem = (CloudAppendBlob)item;
                        Assert.AreEqual(blobItem.Name, BlobName);
                        Assert.IsFalse(blobItem.IsDeleted);
                        Assert.IsNull(blobItem.Properties.DeletedTime);
                        Assert.IsNull(blobItem.Properties.RemainingDaysBeforePermanentDelete);
                        blobCount++;
                    }
                } while (ct != null);
                Assert.AreEqual(blobCount, 1);

            }
            finally
            {
                container.ServiceClient.DisableSoftDelete();
                container.DeleteIfExistsAsync().Wait();
            }
        }

        [TestMethod]
        [Description("Soft Delete and Undelete a blob with snapshots TASK")]
        [TestCategory(ComponentCategory.Blob)]
        [TestCategory(TestTypeCategory.UnitTest)]
        [TestCategory(SmokeTestCategory.NonSmoke)]
        [TestCategory(TenantTypeCategory.DevStore), TestCategory(TenantTypeCategory.DevFabric), TestCategory(TenantTypeCategory.Cloud)]
        public void CloudBlobSoftDeleteSnapshotTask()
        {
            CloudBlobContainer container = GetRandomContainerReference();
            try
            {
                //Enables a delete retention policy on the blob with 1 day of default retention days
                container.ServiceClient.EnableSoftDelete();
                container.CreateAsync().Wait();

                // Upload some data to the blob.
                MemoryStream originalData = new MemoryStream(GetRandomBuffer(1024));
                CloudAppendBlob appendBlob = container.GetAppendBlobReference(BlobName);
                appendBlob.UploadFromStreamAsync(originalData).Wait();

                CloudBlob blob = container.GetBlobReference(BlobName);

                //create snapshot via api
                CloudBlob snapshot = blob.SnapshotAsync().Result;
                //create snapshot via write protection
                appendBlob.UploadFromStreamAsync(originalData).Wait();

                //we should have 2 snapshots 1 regular and 1 deleted: there is no way to get only the deleted snapshots but the below listing will get both snapshot types
                int blobCount = 0;
                int deletedSnapshotCount = 0;
                int snapShotCount = 0;
                BlobContinuationToken ct = null;
                do
                {
                    foreach (IListBlobItem item in container.ListBlobsSegmentedAsync
                        (null, true, BlobListingDetails.Snapshots | BlobListingDetails.Deleted, null, ct, null, null)
                        .Result
                        .Results
                        .ToList())
                    {
                        CloudAppendBlob blobItem = (CloudAppendBlob)item;
                        Assert.AreEqual(blobItem.Name, BlobName);
                        if (blobItem.IsSnapshot)
                            snapShotCount++;
                        if (blobItem.IsDeleted)
                        {
                            Assert.IsNotNull(blobItem.Properties.DeletedTime);
                            Assert.AreEqual(blobItem.Properties.RemainingDaysBeforePermanentDelete, 0);
                            deletedSnapshotCount++;
                        }
                        blobCount++;
                    }
                } while (ct != null);

                Assert.AreEqual(blobCount, 3);
                Assert.AreEqual(deletedSnapshotCount, 1);
                Assert.AreEqual(snapShotCount, 2);

                blobCount = 0;
                ct = null;
                do
                {
                    foreach (IListBlobItem item in container.ListBlobsSegmentedAsync
                        (null, true, BlobListingDetails.Snapshots, null, ct, null, null)
                        .Result
                        .Results
                        .ToList())
                    {
                        CloudAppendBlob blobItem = (CloudAppendBlob)item;
                        Assert.AreEqual(blobItem.Name, BlobName);
                        Assert.IsFalse(blobItem.IsDeleted);
                        Assert.IsNull(blobItem.Properties.DeletedTime);
                        Assert.IsNull(blobItem.Properties.RemainingDaysBeforePermanentDelete);
                        blobCount++;
                    }
                } while (ct != null);

                Assert.AreEqual(blobCount, 2);

                //Delete Blob and snapshots
                blob.DeleteAsync(DeleteSnapshotsOption.IncludeSnapshots,null, null, null).Wait();
                Assert.IsFalse(blob.ExistsAsync().Result);
                Assert.IsFalse(snapshot.ExistsAsync().Result);

                blobCount = 0;
                ct = null;
                do
                {
                    foreach (IListBlobItem item in container.ListBlobsSegmentedAsync
                        (null, true, BlobListingDetails.All, null, ct, null, null)
                        .Result
                        .Results
                        .ToList())
                    {
                        CloudAppendBlob blobItem = (CloudAppendBlob)item;
                        Assert.AreEqual(blobItem.Name, BlobName);
                        Assert.IsTrue(blobItem.IsDeleted);
                        Assert.IsNotNull(blobItem.Properties.DeletedTime);
                        Assert.AreEqual(blobItem.Properties.RemainingDaysBeforePermanentDelete, 0);
                        blobCount++;
                    }
                } while (ct != null);

                Assert.AreEqual(blobCount, 3);

                blob.UndeleteAsync().Wait();

                blob.FetchAttributesAsync().Wait();
                Assert.IsFalse(blob.IsDeleted);
                Assert.IsNull(blob.Properties.DeletedTime);
                Assert.IsNull(blob.Properties.RemainingDaysBeforePermanentDelete);

                blobCount = 0;
                ct = null;
                do
                {
                    foreach (IListBlobItem item in container.ListBlobsSegmentedAsync
                        (null, true, BlobListingDetails.All, null, ct, null, null)
                        .Result
                        .Results
                        .ToList())
                    {
                        CloudAppendBlob blobItem = (CloudAppendBlob)item;
                        Assert.AreEqual(blobItem.Name, BlobName);
                        Assert.IsFalse(blobItem.IsDeleted);
                        Assert.IsNull(blobItem.Properties.DeletedTime);
                        Assert.IsNull(blobItem.Properties.RemainingDaysBeforePermanentDelete);
                        blobCount++;
                    }
                } while (ct != null);

                Assert.AreEqual(blobCount, 3);

            }
            finally
            {
                container.ServiceClient.DisableSoftDelete();
                container.DeleteIfExistsAsync().Wait();
            }
        }
        #endregion
        #endregion
    }
}
