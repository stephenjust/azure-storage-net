﻿//-----------------------------------------------------------------------
// <copyright file="CloudFileClient.cs" company="Microsoft">
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
//-----------------------------------------------------------------------

namespace Microsoft.Azure.Storage.File
{
    using Microsoft.Azure.Storage.Core;
    using Microsoft.Azure.Storage.Core.Executor;
    using Microsoft.Azure.Storage.Core.Util;
    using Microsoft.Azure.Storage.File.Protocol;
    using Microsoft.Azure.Storage.Shared.Protocol;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides a client-side logical representation of the Microsoft Azure File service. This client is used to configure and execute requests against the File service.
    /// </summary>
    /// <remarks>The service client encapsulates the base URI for the File service. If the service client will be used for authenticated access,
    /// it also encapsulates the credentials for accessing the storage account.</remarks>
    public partial class CloudFileClient
    {
        /// <summary>
        /// Gets or sets the authentication scheme to use to sign HTTP requests.
        /// </summary>
        public AuthenticationScheme AuthenticationScheme
        {
            get
            {
                return this.authenticationScheme;
            }

            set
            {
                this.authenticationScheme = value;
            }
        }

#if SYNC
        /// <summary>
        /// Returns an enumerable collection of shares, which are retrieved lazily, whose names 
        /// begin with the specified prefix.
        /// </summary>
        /// <param name="prefix">The share name prefix.</param>
        /// <param name="detailsIncluded">A value that indicates whether to return share metadata with the listing.</param>
        /// <param name="options">A <see cref="FileRequestOptions"/> object that specifies additional options for the request.</param>
        /// <param name="operationContext">An <see cref="OperationContext"/> object that represents the context for the current operation.</param>
        /// <returns>An enumerable collection of shares that are retrieved lazily.</returns>
        [DoesServiceRequest]
        public virtual IEnumerable<CloudFileShare> ListShares(string prefix = null, ShareListingDetails detailsIncluded = ShareListingDetails.None, FileRequestOptions options = null, OperationContext operationContext = null)
        {
            FileRequestOptions modifiedOptions = FileRequestOptions.ApplyDefaults(options, this);
            return CommonUtility.LazyEnumerable(
                token => this.ListSharesSegmentedCore(prefix, detailsIncluded, null, (FileContinuationToken)token, modifiedOptions, operationContext),
                long.MaxValue);
        }

        /// <summary>
        /// Returns a result segment containing a collection of shares.
        /// </summary>
        /// <param name="currentToken">A <see cref="FileContinuationToken"/> token returned by a previous listing operation.</param>
        /// <returns>A result segment of shares.</returns>
        [DoesServiceRequest]
        public virtual ShareResultSegment ListSharesSegmented(FileContinuationToken currentToken)
        {
            return this.ListSharesSegmented(null, ShareListingDetails.None, null, currentToken, null, null);
        }

        /// <summary>
        /// Returns a result segment containing a collection of shares.
        /// </summary>
        /// <param name="prefix">The share name prefix.</param>
        /// <param name="currentToken">A continuation token returned by a previous listing operation.</param> 
        /// <returns>A result segment of shares.</returns>
        [DoesServiceRequest]
        public virtual ShareResultSegment ListSharesSegmented(string prefix, FileContinuationToken currentToken)
        {
            return this.ListSharesSegmented(prefix, ShareListingDetails.None, null, currentToken, null, null);
        }

        /// <summary>
        /// Returns a result segment containing a collection of shares
        /// whose names begin with the specified prefix.
        /// </summary>
        /// <param name="prefix">The share name prefix.</param>
        /// <param name="detailsIncluded">A value that indicates whether to return share metadata with the listing.</param>
        /// <param name="maxResults">A non-negative integer value that indicates the maximum number of results to be returned 
        /// in the result segment, up to the per-operation limit of 5000. If this value is null, the maximum possible number of results will be returned, up to 5000.</param>         
        /// <param name="currentToken">A continuation token returned by a previous listing operation.</param> 
        /// <param name="options">A <see cref="FileRequestOptions"/> object that specifies additional options for the request.</param>
        /// <param name="operationContext">An <see cref="OperationContext"/> object that represents the context for the current operation.</param>
        /// <returns>A result segment of shares.</returns>
        [DoesServiceRequest]
        public virtual ShareResultSegment ListSharesSegmented(string prefix, ShareListingDetails detailsIncluded, int? maxResults, FileContinuationToken currentToken, FileRequestOptions options = null, OperationContext operationContext = null)
        {
            FileRequestOptions modifiedOptions = FileRequestOptions.ApplyDefaults(options, this);
            ResultSegment<CloudFileShare> resultSegment = this.ListSharesSegmentedCore(prefix, detailsIncluded, maxResults, currentToken, modifiedOptions, operationContext);
            return new ShareResultSegment(resultSegment.Results, (FileContinuationToken)resultSegment.ContinuationToken);
        }

        /// <summary>
        /// Returns a result segment containing a collection of shares
        /// whose names begin with the specified prefix.
        /// </summary>
        /// <param name="prefix">The share name prefix.</param>
        /// <param name="detailsIncluded">A value that indicates whether to return share metadata with the listing.</param>
        /// <param name="maxResults">A non-negative integer value that indicates the maximum number of results to be returned 
        /// in the result segment, up to the per-operation limit of 5000. If this value is null, the maximum possible number of results will be returned, up to 5000.</param>         
        /// <param name="currentToken">A continuation token returned by a previous listing operation.</param> 
        /// <param name="options">A <see cref="FileRequestOptions"/> object that specifies additional options for the request.</param>
        /// <param name="operationContext">An <see cref="OperationContext"/> object that represents the context for the current operation.</param>
        /// <returns>A result segment of shares.</returns>
        private ResultSegment<CloudFileShare> ListSharesSegmentedCore(string prefix, ShareListingDetails detailsIncluded, int? maxResults, FileContinuationToken currentToken, FileRequestOptions options, OperationContext operationContext)
        {
            return Executor.ExecuteSync(
                this.ListSharesImpl(prefix, detailsIncluded, currentToken, maxResults, options),
                options.RetryPolicy, 
                operationContext);
        }
#endif

        /// <summary>
        /// Begins an asynchronous request to return a result segment containing a collection of shares.
        /// </summary>
        /// <param name="currentToken">A continuation token returned by a previous listing operation.</param> 
        /// <param name="callback">The callback delegate that will receive notification when the asynchronous operation completes.</param>
        /// <param name="state">A user-defined object that will be passed to the callback delegate.</param>
        /// <returns>An <see cref="IAsyncResult"/> that references the asynchronous operation.</returns>
        [DoesServiceRequest]
        public virtual ICancellableAsyncResult BeginListSharesSegmented(FileContinuationToken currentToken, AsyncCallback callback, object state)
        {
            return this.BeginListSharesSegmented(null, ShareListingDetails.None, null, currentToken, null, null, callback, state);
        }

        /// <summary>
        /// Begins an asynchronous request to return a result segment containing a collection of shares.
        /// </summary>
        /// <param name="prefix">The share name prefix.</param>
        /// <param name="currentToken">A continuation token returned by a previous listing operation.</param> 
        /// <param name="callback">The callback delegate that will receive notification when the asynchronous operation completes.</param>
        /// <param name="state">A user-defined object that will be passed to the callback delegate.</param>
        /// <returns>An <see cref="IAsyncResult"/> that references the asynchronous operation.</returns>
        [DoesServiceRequest]
        public virtual ICancellableAsyncResult BeginListSharesSegmented(string prefix, FileContinuationToken currentToken, AsyncCallback callback, object state)
        {
            return this.BeginListSharesSegmented(prefix, ShareListingDetails.None, null, currentToken, null, null, callback, state);
        }

        /// <summary>
        /// Begins an asynchronous request to return a result segment containing a collection of shares
        /// whose names begin with the specified prefix.
        /// </summary>
        /// <param name="prefix">The share name prefix.</param>
        /// <param name="detailsIncluded">A value that indicates whether to return share metadata with the listing.</param>
        /// <param name="maxResults">A non-negative integer value that indicates the maximum number of results to be returned 
        /// in the result segment, up to the per-operation limit of 5000. If this value is null, the maximum possible number of results will be returned, up to 5000.</param>         
        /// <param name="currentToken">A continuation token returned by a previous listing operation.</param> 
        /// <param name="options">A <see cref="FileRequestOptions"/> object that specifies additional options for the request.</param>
        /// <param name="operationContext">An <see cref="OperationContext"/> object that represents the context for the current operation.</param>
        /// <param name="callback">The callback delegate that will receive notification when the asynchronous operation completes.</param>
        /// <param name="state">A user-defined object that will be passed to the callback delegate.</param>
        /// <returns>An <see cref="IAsyncResult"/> that references the asynchronous operation.</returns>
        [DoesServiceRequest]
        public virtual ICancellableAsyncResult BeginListSharesSegmented(string prefix, ShareListingDetails detailsIncluded, int? maxResults, FileContinuationToken currentToken, FileRequestOptions options, OperationContext operationContext, AsyncCallback callback, object state)
        {
            FileRequestOptions modifiedOptions = FileRequestOptions.ApplyDefaults(options, this);

            return CancellableAsyncResultTaskWrapper.Create(token => this.ListSharesSegmentedAsync(prefix, detailsIncluded, maxResults, currentToken, modifiedOptions, operationContext), callback, state);
        }

        /// <summary>
        /// Ends an asynchronous operation to return a result segment containing a collection of shares.
        /// </summary>
        /// <param name="asyncResult">An <see cref="IAsyncResult"/> that references the pending asynchronous operation.</param>
        /// <returns>A result segment of shares.</returns>
        public virtual ShareResultSegment EndListSharesSegmented(IAsyncResult asyncResult)
        {
            CommonUtility.AssertNotNull(nameof(asyncResult), asyncResult);
            ShareResultSegment resultSegment = ((CancellableAsyncResultTaskWrapper<ShareResultSegment>)(asyncResult)).GetAwaiter().GetResult();
            return new ShareResultSegment(resultSegment.Results, resultSegment.ContinuationToken);
        }
        
#if TASK
        /// <summary>
        /// Returns a task that performs an asynchronous request to return a result segment containing a collection of shares.
        /// </summary>
        /// <param name="currentToken">A <see cref="FileContinuationToken"/> token returned by a previous listing operation.</param>
        /// <returns>A <see cref="Task{T}"/> object that represents the current operation.</returns>
        [DoesServiceRequest]
        public virtual Task<ShareResultSegment> ListSharesSegmentedAsync(FileContinuationToken currentToken)
        {
            return this.ListSharesSegmentedAsync(currentToken, CancellationToken.None);
        }

        /// <summary>
        /// Returns a task that performs an asynchronous request to return a result segment containing a collection of shares.
        /// </summary>
        /// <param name="currentToken">A <see cref="FileContinuationToken"/> token returned by a previous listing operation.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for a task to complete.</param>
        /// <returns>A <see cref="Task{T}"/> object that represents the current operation.</returns>
        [DoesServiceRequest]
        public virtual Task<ShareResultSegment> ListSharesSegmentedAsync(FileContinuationToken currentToken, CancellationToken cancellationToken)
        {
            return ListSharesSegmentedAsync(default(string) /*prefix*/, currentToken, cancellationToken);
        }

        /// <summary>
        /// Returns a task that performs an asynchronous request to return a result segment containing a collection of shares
        /// whose names begin with the specified prefix.
        /// </summary>
        /// <param name="prefix">The share name prefix.</param>    
        /// <param name="currentToken">A continuation token returned by a previous listing operation.</param> 
        /// <returns>A <see cref="Task{T}"/> object that represents the current operation.</returns>
        [DoesServiceRequest]
        public virtual Task<ShareResultSegment> ListSharesSegmentedAsync(string prefix, FileContinuationToken currentToken)
        {
            return this.ListSharesSegmentedAsync(prefix, currentToken, CancellationToken.None);
        }

        /// <summary>
        /// Returns a task that performs an asynchronous request to return a result segment containing a collection of shares
        /// whose names begin with the specified prefix.
        /// </summary>
        /// <param name="prefix">The share name prefix.</param>    
        /// <param name="currentToken">A continuation token returned by a previous listing operation.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for a task to complete.</param>
        /// <returns>A <see cref="Task{T}"/> object that represents the current operation.</returns>
        [DoesServiceRequest]
        public virtual Task<ShareResultSegment> ListSharesSegmentedAsync(string prefix, FileContinuationToken currentToken, CancellationToken cancellationToken)
        {
            return this.ListSharesSegmentedAsync(prefix, default(ShareListingDetails), default(int?) /*maxResults*/, currentToken, default(FileRequestOptions), default(OperationContext), cancellationToken);
        }        
        /// <summary>
        /// Returns a task that performs an asynchronous request to return a result segment containing a collection of shares
        /// whose names begin with the specified prefix.
        /// </summary>
        /// <param name="prefix">The share name prefix.</param>
        /// <param name="detailsIncluded">A value that indicates whether to return share metadata with the listing.</param>
        /// <param name="maxResults">A non-negative integer value that indicates the maximum number of results to be returned 
        /// in the result segment, up to the per-operation limit of 5000. If this value is null, the maximum possible number of results will be returned, up to 5000.</param>         
        /// <param name="currentToken">A continuation token returned by a previous listing operation.</param> 
        /// <param name="options">A <see cref="FileRequestOptions"/> object that specifies additional options for the request.</param>
        /// <param name="operationContext">An <see cref="OperationContext"/> object that represents the context for the current operation.</param>
        /// <returns>A <see cref="Task{T}"/> object that represents the current operation.</returns>
        [DoesServiceRequest]
        public virtual Task<ShareResultSegment> ListSharesSegmentedAsync(string prefix, ShareListingDetails detailsIncluded, int? maxResults, FileContinuationToken currentToken, FileRequestOptions options, OperationContext operationContext)
        {
            return this.ListSharesSegmentedAsync(prefix, detailsIncluded, maxResults, currentToken, options, operationContext, CancellationToken.None);
        }
        
        /// <summary>
        /// Returns a task that performs an asynchronous request to return a result segment containing a collection of shares
        /// whose names begin with the specified prefix.
        /// </summary>
        /// <param name="prefix">The share name prefix.</param>
        /// <param name="detailsIncluded">A value that indicates whether to return share metadata with the listing.</param>
        /// <param name="maxResults">A non-negative integer value that indicates the maximum number of results to be returned 
        /// in the result segment, up to the per-operation limit of 5000. If this value is null, the maximum possible number of results will be returned, up to 5000.</param>         
        /// <param name="currentToken">A continuation token returned by a previous listing operation.</param> 
        /// <param name="options">A <see cref="FileRequestOptions"/> object that specifies additional options for the request.</param>
        /// <param name="operationContext">An <see cref="OperationContext"/> object that represents the context for the current operation.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for a task to complete.</param>
        /// <returns>A <see cref="Task{T}"/> object that represents the current operation.</returns>
        [DoesServiceRequest]
        public virtual async Task<ShareResultSegment> ListSharesSegmentedAsync(string prefix, ShareListingDetails detailsIncluded, int? maxResults, FileContinuationToken currentToken, FileRequestOptions options, OperationContext operationContext, CancellationToken cancellationToken)
        {
            FileRequestOptions modifiedOptions = FileRequestOptions.ApplyDefaults(options, this);
            ResultSegment<CloudFileShare> resultSegment = await Executor.ExecuteAsync(
                this.ListSharesImpl(prefix, detailsIncluded, currentToken, maxResults, modifiedOptions),
                modifiedOptions.RetryPolicy,
                operationContext,
                cancellationToken).ConfigureAwait(false);
            return new ShareResultSegment(resultSegment.Results, (FileContinuationToken)resultSegment.ContinuationToken);
        }
#endif

        /// <summary>
        /// Begins an asynchronous operation to get service properties for the File service.
        /// </summary>
        /// <param name="callback">An <see cref="AsyncCallback"/> delegate that will receive notification when the asynchronous operation completes.</param>
        /// <param name="state">A user-defined object to be passed to the callback delegate.</param>
        /// <returns>An <see cref="ICancellableAsyncResult"/> that references the asynchronous operation.</returns>
        [DoesServiceRequest]
        public virtual ICancellableAsyncResult BeginGetServiceProperties(AsyncCallback callback, object state)
        {
            return this.BeginGetServiceProperties(null /* requestOptions */, null /* operationContext */, callback, state);
        }

        /// <summary>
        /// Begins an asynchronous operation to get service properties for the File service.
        /// </summary>
        /// <param name="requestOptions">A <see cref="FileRequestOptions"/> object that specifies additional options for the request.</param>
        /// <param name="operationContext">An <see cref="OperationContext"/> object that represents the context for the current operation.</param>
        /// <param name="callback">An <see cref="AsyncCallback"/> delegate that will receive notification when the asynchronous operation completes.</param>
        /// <param name="state">A user-defined object to be passed to the callback delegate.</param>
        /// <returns>An <see cref="ICancellableAsyncResult"/> that references the asynchronous operation.</returns>
        [DoesServiceRequest]
        public virtual ICancellableAsyncResult BeginGetServiceProperties(FileRequestOptions requestOptions, OperationContext operationContext, AsyncCallback callback, object state)
        {
            requestOptions = FileRequestOptions.ApplyDefaults(requestOptions, this);
            operationContext = operationContext ?? new OperationContext();

            return CancellableAsyncResultTaskWrapper.Create(token => this.GetServicePropertiesAsync(requestOptions, operationContext), callback, state);
        }

        /// <summary>
        /// Ends an asynchronous operation to get service properties for the File service.
        /// </summary>
        /// <param name="asyncResult">An <see cref="IAsyncResult"/> that references the pending asynchronous operation.</param>
        /// <returns>A <see cref="FileServiceProperties"/> object.</returns>
        public virtual FileServiceProperties EndGetServiceProperties(IAsyncResult asyncResult)
        {
            CommonUtility.AssertNotNull(nameof(asyncResult), asyncResult);
            return ((CancellableAsyncResultTaskWrapper<FileServiceProperties>)(asyncResult)).GetAwaiter().GetResult();
        }

#if TASK
        /// <summary>
        /// Initiates an asynchronous operation to get service properties for the File service.
        /// </summary>
        /// <returns>A <see cref="Task{T}"/> object of type <see cref="FileServiceProperties"/> that represents the asynchronous operation.</returns>
        [DoesServiceRequest]
        public virtual Task<FileServiceProperties> GetServicePropertiesAsync()
        {
            return this.GetServicePropertiesAsync(CancellationToken.None);
        }

        /// <summary>
        /// Initiates an asynchronous operation to get service properties for the File service.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for a task to complete.</param>
        /// <returns>A <see cref="Task{T}"/> object of type <see cref="FileServiceProperties"/> that represents the asynchronous operation.</returns>
        [DoesServiceRequest]
        public virtual Task<FileServiceProperties> GetServicePropertiesAsync(CancellationToken cancellationToken)
        {
            return this.GetServicePropertiesAsync(default(FileRequestOptions), default(OperationContext), cancellationToken);
        }

        /// <summary>
        /// Initiates an asynchronous operation to get service properties for the File service.
        /// </summary>
        /// <param name="requestOptions">A <see cref="FileRequestOptions"/> object that specifies additional options for the request.</param>
        /// <param name="operationContext">An <see cref="OperationContext"/> object that represents the context for the current operation.</param>
        /// <returns>A <see cref="Task{T}"/> object of type <see cref="FileServiceProperties"/> that represents the asynchronous operation.</returns>
        [DoesServiceRequest]
        public virtual Task<FileServiceProperties> GetServicePropertiesAsync(FileRequestOptions requestOptions, OperationContext operationContext)
        {
            return this.GetServicePropertiesAsync(requestOptions, operationContext, CancellationToken.None);
        }

        /// <summary>
        /// Initiates an asynchronous operation to get service properties for the File service.
        /// </summary>
        /// <param name="requestOptions">A <see cref="FileRequestOptions"/> object that specifies additional options for the request.</param>
        /// <param name="operationContext">An <see cref="OperationContext"/> object that represents the context for the current operation.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for a task to complete.</param>
        /// <returns>A <see cref="Task{T}"/> object of type <see cref="FileServiceProperties"/> that represents the asynchronous operation.</returns>
        [DoesServiceRequest]
        public virtual Task<FileServiceProperties> GetServicePropertiesAsync(FileRequestOptions requestOptions, OperationContext operationContext, CancellationToken cancellationToken)
        {
            requestOptions = FileRequestOptions.ApplyDefaults(requestOptions, this);
            operationContext = operationContext ?? new OperationContext();
            return Executor.ExecuteAsync(
                this.GetServicePropertiesImpl(requestOptions),
                requestOptions.RetryPolicy,
                operationContext,
                cancellationToken);
        }
#endif

#if SYNC
        /// <summary>
        /// Gets service properties for the File service.
        /// </summary>
        /// <param name="requestOptions">A <see cref="FileRequestOptions"/> object that specifies additional options for the request.</param>
        /// <param name="operationContext">An <see cref="OperationContext"/> object that represents the context for the current operation.</param>
        /// <returns>A <see cref="FileServiceProperties"/> object.</returns>
        [DoesServiceRequest]
        public virtual FileServiceProperties GetServiceProperties(FileRequestOptions requestOptions = null, OperationContext operationContext = null)
        {
            requestOptions = FileRequestOptions.ApplyDefaults(requestOptions, this);
            operationContext = operationContext ?? new OperationContext();
            return Executor.ExecuteSync(
                this.GetServicePropertiesImpl(requestOptions),
                requestOptions.RetryPolicy,
                operationContext);
        }
#endif

        /// <summary>
        /// Begins an asynchronous operation to set service properties for the File service.
        /// </summary>
        /// <param name="properties">A <see cref="FileServiceProperties"/> object.</param>
        /// <param name="callback">An <see cref="AsyncCallback"/> delegate that will receive notification when the asynchronous operation completes.</param>
        /// <param name="state">A user-defined object to be passed to the callback delegate.</param>
        /// <returns>An <see cref="ICancellableAsyncResult"/> that references the asynchronous operation.</returns>
        [DoesServiceRequest]
        public virtual ICancellableAsyncResult BeginSetServiceProperties(FileServiceProperties properties, AsyncCallback callback, object state)
        {
            return this.BeginSetServiceProperties(properties, null /* requestOptions */, null /* operationContext */, callback, state);
        }

        /// <summary>
        /// Begins an asynchronous operation to set service properties for the File service.
        /// </summary>
        /// <param name="properties">A <see cref="FileServiceProperties"/> object.</param>
        /// <param name="requestOptions">A <see cref="FileRequestOptions"/> object that specifies additional options for the request.</param>
        /// <param name="operationContext">An <see cref="OperationContext"/> object that represents the context for the current operation.</param>
        /// <param name="callback">An <see cref="AsyncCallback"/> delegate that will receive notification when the asynchronous operation completes.</param>
        /// <param name="state">A user-defined object to be passed to the callback delegate.</param>
        /// <returns>An <see cref="ICancellableAsyncResult"/> that references the asynchronous operation.</returns>
        [DoesServiceRequest]
        public virtual ICancellableAsyncResult BeginSetServiceProperties(FileServiceProperties properties, FileRequestOptions requestOptions, OperationContext operationContext, AsyncCallback callback, object state)
        {
            requestOptions = FileRequestOptions.ApplyDefaults(requestOptions, this);
            operationContext = operationContext ?? new OperationContext();

            return CancellableAsyncResultTaskWrapper.Create(token => this.SetServicePropertiesAsync(properties, requestOptions, operationContext), callback, state);
        }

        /// <summary>
        /// Ends an asynchronous operation to set service properties for the Blob service.
        /// </summary>
        /// <param name="asyncResult">An <see cref="IAsyncResult"/> that references the pending asynchronous operation.</param>
        public virtual void EndSetServiceProperties(IAsyncResult asyncResult)
        {
            CommonUtility.AssertNotNull(nameof(asyncResult), asyncResult);
            ((CancellableAsyncResultTaskWrapper)(asyncResult)).GetAwaiter().GetResult();
        }

#if TASK
        /// <summary>
        /// Initiates an asynchronous operation that sets service properties for the Blob service.
        /// </summary>
        /// <param name="properties">A <see cref="FileServiceProperties"/> object.</param>
        /// <returns>A <see cref="Task"/> object that represents the asynchronous operation.</returns>
        [DoesServiceRequest]
        public virtual Task SetServicePropertiesAsync(FileServiceProperties properties)
        {
            return this.SetServicePropertiesAsync(properties, CancellationToken.None);
        }

        /// <summary>
        /// Initiates an asynchronous operation that sets service properties for the Blob service.
        /// </summary>
        /// <param name="properties">A <see cref="FileServiceProperties"/> object.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for a task to complete.</param>
        /// <returns>A <see cref="Task"/> object that represents the asynchronous operation.</returns>
        [DoesServiceRequest]
        public virtual Task SetServicePropertiesAsync(FileServiceProperties properties, CancellationToken cancellationToken)
        {
            return this.SetServicePropertiesAsync(properties, default(FileRequestOptions), default(OperationContext), cancellationToken);
        }

        /// <summary>
        /// Initiates an asynchronous operation that sets service properties for the File service.
        /// </summary>
        /// <param name="properties">A <see cref="FileServiceProperties"/> object.</param>
        /// <param name="requestOptions">A <see cref="FileRequestOptions"/> object that specifies additional options for the request.</param>
        /// <param name="operationContext">An <see cref="OperationContext"/> object that represents the context for the current operation.</param>
        /// <returns>A <see cref="Task"/> object that represents the asynchronous operation.</returns>
        [DoesServiceRequest]
        public virtual Task SetServicePropertiesAsync(FileServiceProperties properties, FileRequestOptions requestOptions, OperationContext operationContext)
        {
            return this.SetServicePropertiesAsync(properties, requestOptions, operationContext, CancellationToken.None);
        }

        /// <summary>
        /// Initiates an asynchronous operation that sets service properties for the File service.
        /// </summary>
        /// <param name="properties">A <see cref="FileServiceProperties"/> object.</param>
        /// <param name="requestOptions">A <see cref="FileRequestOptions"/> object that specifies additional options for the request.</param>
        /// <param name="operationContext">An <see cref="OperationContext"/> object that represents the context for the current operation.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for a task to complete.</param>
        /// <returns>A <see cref="Task"/> object that represents the asynchronous operation.</returns>
        [DoesServiceRequest]
        public virtual Task SetServicePropertiesAsync(FileServiceProperties properties, FileRequestOptions requestOptions, OperationContext operationContext, CancellationToken cancellationToken)
        {
            requestOptions = FileRequestOptions.ApplyDefaults(requestOptions, this);
            operationContext = operationContext ?? new OperationContext();
            return Executor.ExecuteAsync(
                this.SetServicePropertiesImpl(properties, requestOptions),
                requestOptions.RetryPolicy,
                operationContext,
                cancellationToken);
        }
#endif

#if SYNC
        /// <summary>
        /// Sets service properties for the File service.
        /// </summary>
        /// <param name="properties">A <see cref="FileServiceProperties"/> object.</param>
        /// <param name="requestOptions">A <see cref="FileRequestOptions"/> object that specifies additional options for the request.</param>
        /// <param name="operationContext">An <see cref="OperationContext"/> object that represents the context for the current operation.</param>
        [DoesServiceRequest]
        public virtual void SetServiceProperties(FileServiceProperties properties, FileRequestOptions requestOptions = null, OperationContext operationContext = null)
        {
            requestOptions = FileRequestOptions.ApplyDefaults(requestOptions, this);
            operationContext = operationContext ?? new OperationContext();
            Executor.ExecuteSync(
                this.SetServicePropertiesImpl(properties, requestOptions),
                requestOptions.RetryPolicy,
                operationContext);
        }
#endif

        /// <summary>
        /// Core implementation for the ListShares method.
        /// </summary>
        /// <param name="prefix">The share prefix.</param>
        /// <param name="detailsIncluded">The details included.</param>
        /// <param name="currentToken">The continuation token.</param>
        /// <param name="maxResults">A non-negative integer value that indicates the maximum number of results to be returned 
        /// in the result segment, up to the per-operation limit of 5000. If this value is null, the maximum possible number of results will be returned, up to 5000.</param>
        /// <param name="options">A <see cref="FileRequestOptions"/> object that specifies additional options for the request.</param>
        /// <returns>A <see cref="RESTCommand{T}"/> that lists the shares.</returns>
        private RESTCommand<ResultSegment<CloudFileShare>> ListSharesImpl(string prefix, ShareListingDetails detailsIncluded, FileContinuationToken currentToken, int? maxResults, FileRequestOptions options)
        {
            ListingContext listingContext = new ListingContext(prefix, maxResults)
            {
                Marker = currentToken != null ? currentToken.NextMarker : null
            };

            RESTCommand<ResultSegment<CloudFileShare>> getCmd = new RESTCommand<ResultSegment<CloudFileShare>>(this.Credentials, this.StorageUri, this.HttpClient);

            options.ApplyToStorageCommand(getCmd);
            getCmd.CommandLocationMode = CommonUtility.GetListingLocationMode(currentToken);
            getCmd.RetrieveResponseStream = true;
            getCmd.BuildRequest = (cmd, uri, builder, cnt, serverTimeout, ctx) => ShareHttpRequestMessageFactory.List(uri, serverTimeout, listingContext, detailsIncluded, cnt, ctx, this.GetCanonicalizer(), this.Credentials);
            getCmd.PreProcessResponse = (cmd, resp, ex, ctx) => HttpResponseParsers.ProcessExpectedStatusCodeNoException(HttpStatusCode.OK, resp, null, cmd, ex);
            getCmd.PostProcessResponseAsync = async (cmd, resp, ctx, ct) =>
            {
                ListSharesResponse listSharesResponse = await ListSharesResponse.ParseAsync(cmd.ResponseStream, ct).ConfigureAwait(false);
                List<CloudFileShare> sharesList = listSharesResponse.Shares.Select(item => new CloudFileShare(item.Properties, item.Metadata, item.Name, item.SnapshotTime, this)).ToList();
                FileContinuationToken continuationToken = null;
                if (listSharesResponse.NextMarker != null)
                {
                    continuationToken = new FileContinuationToken()
                    {
                        NextMarker = listSharesResponse.NextMarker,
                        TargetLocation = cmd.CurrentResult.TargetLocation,
                    };
                }

                return new ResultSegment<CloudFileShare>(sharesList)
                {
                    ContinuationToken = continuationToken,
                };
            };

            return getCmd;
        }

        private RESTCommand<FileServiceProperties> GetServicePropertiesImpl(FileRequestOptions requestOptions)
        {
            RESTCommand<FileServiceProperties> retCmd = new RESTCommand<FileServiceProperties>(this.Credentials, this.StorageUri, this.HttpClient);

            retCmd.CommandLocationMode = CommandLocationMode.PrimaryOrSecondary;
            retCmd.BuildRequest = (cmd, uri, builder, cnt, serverTimeout, ctx) => FileHttpRequestMessageFactory.GetServiceProperties(uri, serverTimeout, ctx, this.GetCanonicalizer(), this.Credentials);
            retCmd.RetrieveResponseStream = true;
            retCmd.PreProcessResponse =
                (cmd, resp, ex, ctx) =>
                HttpResponseParsers.ProcessExpectedStatusCodeNoException(HttpStatusCode.OK, resp, null /* retVal */, cmd, ex);

            retCmd.PostProcessResponseAsync = (cmd, resp, ctx, ct) => FileHttpResponseParsers.ReadServicePropertiesAsync(cmd.ResponseStream, ct);

            requestOptions.ApplyToStorageCommand(retCmd);
            return retCmd;
        }

        private RESTCommand<NullType> SetServicePropertiesImpl(FileServiceProperties properties, FileRequestOptions requestOptions)
        {
            MultiBufferMemoryStream memoryStream = new MultiBufferMemoryStream(this.BufferManager, (int)(1 * Constants.KB));
            try
            {
                properties.WriteServiceProperties(memoryStream);
            }
            catch (InvalidOperationException invalidOpException)
            {
                memoryStream.Dispose();
                throw new ArgumentException(invalidOpException.Message, "properties");
            }

            RESTCommand<NullType> retCmd = new RESTCommand<NullType>(this.Credentials, this.StorageUri, this.HttpClient);
            requestOptions.ApplyToStorageCommand(retCmd);
            retCmd.BuildRequest = (cmd, uri, builder, cnt, serverTimeout, ctx) => FileHttpRequestMessageFactory.SetServiceProperties(uri, serverTimeout, cnt, ctx, this.GetCanonicalizer(), this.Credentials);
            retCmd.BuildContent = (cmd, ctx) => HttpContentFactory.BuildContentFromStream(memoryStream, 0, memoryStream.Length, null /* md5 */, cmd, ctx);
            retCmd.StreamToDispose = memoryStream;
            retCmd.RecoveryAction = RecoveryActions.RewindStream;
            retCmd.RetrieveResponseStream = true;
            retCmd.PreProcessResponse =
                (cmd, resp, ex, ctx) =>
                HttpResponseParsers.ProcessExpectedStatusCodeNoException(HttpStatusCode.Accepted, resp, null /* retVal */, cmd, ex);
            return retCmd;
        }
    }
}
