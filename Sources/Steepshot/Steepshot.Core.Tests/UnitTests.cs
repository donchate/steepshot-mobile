﻿using System;
using System.IO;
using NUnit.Framework;
using Steepshot.Core.Authority;
using Steepshot.Core.Models.Requests;
using Steepshot.Core.Utils;

namespace Steepshot.Core.Tests
{
    [TestFixture]
    public class UnitTests : BaseTests
    {
        [Test]
        public void Vote_Empty_Identifier()
        {
            var ex = Assert.Throws<ArgumentNullException>(() =>
            {
                new VoteRequest(new UserInfo { SessionId = "sessionId" }, VoteType.Up, "");
            });
            Assert.That(ex.ParamName, Is.EqualTo("identifier"));
        }

        [Test]
        public void Follow_Empty_Username()
        {
            var ex = Assert.Throws<ArgumentNullException>(() =>
            {
                new FollowRequest(new UserInfo { SessionId = "sessionId" }, FollowType.Follow, "");
            });
            Assert.That(ex.ParamName, Is.EqualTo("username"));
        }

        [Test]
        public void InfoRequest_Empty_Url()
        {
            var ex = Assert.Throws<ArgumentNullException>(() =>
            {
                new InfoRequest("");
            });
            Assert.That(ex.ParamName, Is.EqualTo("url"));
        }

        [Test]
        public void CreateComment_Empty_Url()
        {
            var ex = Assert.Throws<ArgumentNullException>(() =>
            {
                new CreateCommentRequest(new UserInfo { SessionId = "sessionId" }, "", "test", AppSettings.AppInfo);
            });
            Assert.That(ex.ParamName, Is.EqualTo("url"));
        }

        [Test]
        public void Upload_Base64_Equals_ByteArray()
        {
            // Arrange
            var file = File.ReadAllBytes(GetTestImagePath());

            // Act
            var requestArray = new UploadImageRequest(new UserInfo { SessionId = "sessionId" }, "cat" + DateTime.UtcNow.Ticks, file, "cat1", "cat2", "cat3", "cat4");
            var base64 = Convert.ToBase64String(file);
            var requestBase64 = new UploadImageRequest(new UserInfo { SessionId = "sessionId" }, "cat" + DateTime.UtcNow.Ticks, base64, "cat1", "cat2", "cat3", "cat4");

            // Assert
            Assert.That(requestArray.Photo, Is.EqualTo(requestBase64.Photo));
            Assert.That(requestArray.Photo.Length, Is.EqualTo(requestBase64.Photo.Length));
        }

        [Test]
        public void Upload_Empty_Title()
        {
            var ex = Assert.Throws<ArgumentNullException>(() =>
            {
                new UploadImageRequest(new UserInfo { SessionId = "sessionId" }, "", new byte[] { }, "cat1", "cat2", "cat3", "cat4");
                new InfoRequest("");
            });
            Assert.That(ex.ParamName, Is.EqualTo("title"));
        }
    }
}