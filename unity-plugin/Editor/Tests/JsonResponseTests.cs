using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    public class JsonResponseTests
    {
        // ── FormatResponse ───────────────────────────────────────────────────

        [Test]
        public void FormatResponse_Ok_ContainsDataField()
            => Assert.AreEqual("{\"id\":\"x\",\"ok\":true,\"data\":\"hello\"}",
                JsonHelper.FormatResponse("x", true, "hello", null));

        [Test]
        public void FormatResponse_Error_ContainsErrField()
            => Assert.AreEqual("{\"id\":\"x\",\"ok\":false,\"err\":\"oops\"}",
                JsonHelper.FormatResponse("x", false, null, "oops"));

        [Test]
        public void FormatResponse_EscapesQuoteInData()
            => Assert.AreEqual("{\"id\":\"x\",\"ok\":true,\"data\":\"a\\\"b\"}",
                JsonHelper.FormatResponse("x", true, "a\"b", null));

        [Test]
        public void FormatResponse_NullId_EmptyStringId()
            => Assert.AreEqual("{\"id\":\"\",\"ok\":true,\"data\":\"\"}",
                JsonHelper.FormatResponse(null, true, null, null));

        [Test]
        public void FormatResponse_NullData_EmptyDataValue()
            => Assert.AreEqual("{\"id\":\"y\",\"ok\":true,\"data\":\"\"}",
                JsonHelper.FormatResponse("y", true, null, null));

        // ── FormatFileResponse ───────────────────────────────────────────────

        [Test]
        public void FormatFileResponse_IncludesFileField()
            => Assert.AreEqual("{\"id\":\"x\",\"ok\":true,\"data\":\"\",\"file\":\"/p\"}",
                JsonHelper.FormatFileResponse("x", "/p"));

        // ── FormatFileResponseWithData ───────────────────────────────────────

        [Test]
        public void FormatFileResponseWithData_IncludesDataAndFile()
            => Assert.AreEqual("{\"id\":\"x\",\"ok\":true,\"data\":\"d\",\"file\":\"/p\"}",
                JsonHelper.FormatFileResponseWithData("x", "/p", "d"));

        [Test]
        public void FormatFileResponseWithData_NullData_EmptyDataValue()
            => Assert.AreEqual("{\"id\":\"x\",\"ok\":true,\"data\":\"\",\"file\":\"/p\"}",
                JsonHelper.FormatFileResponseWithData("x", "/p", null));

        // ── FormatBusyResponse ───────────────────────────────────────────────

        [Test]
        public void FormatBusyResponse_ContainsRetryField()
            => Assert.AreEqual("{\"id\":\"x\",\"ok\":false,\"err\":\"busy\",\"retry\":200}",
                JsonHelper.FormatBusyResponse("x", "busy", 200));

        [Test]
        public void FormatBusyResponse_EscapesQuoteInMessage()
            => Assert.AreEqual("{\"id\":\"x\",\"ok\":false,\"err\":\"a\\\"b\",\"retry\":100}",
                JsonHelper.FormatBusyResponse("x", "a\"b", 100));
    }
}
