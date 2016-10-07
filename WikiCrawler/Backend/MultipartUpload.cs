using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;

static class MultipartUpload
{
    //UploadFilesToServer(new Uri(Util.UPLOAD_BACKUP), Params, Path.GetFileName(dbFile.Path), "application/octet-stream", fileBytes);

    /// <summary>
    /// Creates HTTP POST request & uploads database to server. Author : Farhan Ghumra
    /// </summary>
    public static Stream UploadFile(HttpWebRequest request, Dictionary<string, string> data, string fileName, string fileContentType, Stream fileData)
    {
        string boundary = "----------" + DateTime.Now.Ticks.ToString("x");
        request.ContentType = "multipart/form-data; boundary=" + boundary;
        request.Method = "POST";
        request.Timeout = 600000;
        Stream requestStream = request.GetRequestStream();
        WriteMultipartForm(requestStream, boundary, data, fileName, fileContentType, fileData);
        requestStream.Flush();
        requestStream.Close();
        return request.GetResponse().GetResponseStream();
    }

    /// <summary>
    /// Writes multi part HTTP POST request. Author : Farhan Ghumra
    /// </summary>
    private static void WriteMultipartForm(Stream s, string boundary, Dictionary<string, string> data, string fileName, string fileContentType, Stream fileData)
    {
        /// The first boundary
        byte[] boundarybytes = Encoding.UTF8.GetBytes("--" + boundary + "\r\n");
        /// the last boundary.
        byte[] trailer = Encoding.UTF8.GetBytes("\r\n--" + boundary + "–-\r\n");
        /// the form data, properly formatted
        string formdataTemplate = "Content-Disposition: form-data; name=\"{0}\"\r\n\r\n{1}";
        /// the form-data file upload, properly formatted
        string fileheaderTemplate = "Content-Disposition: file; name=\"{0}\"; filename=\"{1}\";\r\nContent-Type: {2}\r\n\r\n";

        /// Added to track if we need a CRLF or not.
        bool bNeedsCRLF = false;

        if (data != null)
        {
            foreach (string key in data.Keys)
            {
                /// if we need to drop a CRLF, do that.
                if (bNeedsCRLF)
                    WriteToStream(s, "\r\n");

                /// Write the boundary.
                WriteToStream(s, boundarybytes);

                /// Write the key.
                WriteToStream(s, string.Format(formdataTemplate, key, data[key]));
                bNeedsCRLF = true;
            }
        }

        /// If we don't have keys, we don't need a crlf.
        if (bNeedsCRLF)
            WriteToStream(s, "\r\n");

        WriteToStream(s, boundarybytes);
        WriteToStream(s, string.Format(fileheaderTemplate, "file", fileName, fileContentType));
        /// Write the file data to the stream.
        fileData.CopyTo(s);
        WriteToStream(s, trailer);
    }

    /// <summary>
    /// Writes string to stream. Author : Farhan Ghumra
    /// </summary>
    private static void WriteToStream(Stream s, string txt)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(txt);
        s.Write(bytes, 0, bytes.Length);
    }

    /// <summary>
    /// Writes byte array to stream. Author : Farhan Ghumra
    /// </summary>
    private static void WriteToStream(Stream s, byte[] bytes)
    {
        s.Write(bytes, 0, bytes.Length);
    }
}
