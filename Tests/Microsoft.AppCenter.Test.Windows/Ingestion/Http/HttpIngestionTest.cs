﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AppCenter.Ingestion.Http;
using Microsoft.AppCenter.Ingestion.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.AppCenter.Test.Ingestion.Http
{
    [TestClass]
    public class HttpIngestionTest : IngestionTest
    {
        private HttpIngestion _httpIngestion;

        [TestInitialize]
        public void InitializeHttpIngestionTest()
        {
            _adapter = new Mock<IHttpNetworkAdapter>();
            _httpIngestion = new HttpIngestion(_adapter.Object);
        }

        /// <summary>
        /// Verify that ingestion call http adapter and not fails on success.
        /// </summary>
        [TestMethod]
        public async Task HttpIngestionStatusCodeOk()
        {
            SetupAdapterSendResponse(HttpStatusCode.OK);
            var appSecret = Guid.NewGuid().ToString();
            var installId = Guid.NewGuid();
            var logs = new List<Log>();
            var call = _httpIngestion.Call(appSecret, installId, logs);
            await call.ToTask();
            VerifyAdapterSend(Times.Once);

            // No throw any exception
        }

        /// <summary>
        /// Verify that ingestion throw exception on error response.
        /// </summary>
        [TestMethod]
        public async Task HttpIngestionStatusCodeError()
        {
            SetupAdapterSendResponse(HttpStatusCode.NotFound);
            var appSecret = Guid.NewGuid().ToString();
            var installId = Guid.NewGuid();
            var logs = new List<Log>();
            var call = _httpIngestion.Call(appSecret, installId, logs);
            await Assert.ThrowsExceptionAsync<HttpIngestionException>(() => call.ToTask());
            VerifyAdapterSend(Times.Once);
        }

        /// <summary>
        /// Verify that ingestion don't call http adapter when call is closed.
        /// </summary>
        [TestMethod]
        public async Task HttpIngestionCancel()
        {
            SetupAdapterSendResponse(HttpStatusCode.OK);
            var appSecret = Guid.NewGuid().ToString();
            var installId = Guid.NewGuid();
            var logs = new List<Log>();
            var call = _httpIngestion.Call(appSecret, installId, logs);
            call.Cancel();
            await Assert.ThrowsExceptionAsync<CancellationException>(() => call.ToTask());
            VerifyAdapterSend(Times.Never);
        }

        /// <summary>
        /// Verify that ingestion create headers correctly.
        /// </summary>
        [TestMethod]
        public void HttpIngestionCreateHeaders()
        {
            var appSecret = Guid.NewGuid().ToString();
            var installId = Guid.NewGuid();
            var headers = _httpIngestion.CreateHeaders(appSecret, installId);
            
            Assert.IsTrue(headers.ContainsKey(HttpIngestion.AppSecret));
            Assert.IsTrue(headers.ContainsKey(HttpIngestion.InstallId));
        }
    }
}