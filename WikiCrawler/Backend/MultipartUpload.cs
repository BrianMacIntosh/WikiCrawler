using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

static class MultipartUpload
{
	/// <summary>
	/// Creates HTTP POST request & uploads file to server. Author : Brian MacIntosh
	/// </summary>
	public static Stream UploadFile(HttpWebRequest request, Dictionary<string, string> data,
		string fileName, string fileContentType, string filekey,
		Stream fileData)
	{
		return UploadFile(request, data, fileName, fileContentType, filekey,
			(Stream s) => fileData.CopyTo(s));
	}

	/// <summary>
	/// Creates HTTP POST request & uploads file to server. Author : Brian MacIntosh
	/// </summary>
	public static Stream UploadFile(HttpWebRequest request, Dictionary<string, string> data,
		string fileName, string fileContentType, string filekey,
		byte[] fileData, int fileDataOffset, int fileDataLength)
	{
		return UploadFile(request, data, fileName, fileContentType, filekey,
			(Stream s) => s.Write(fileData, fileDataOffset, fileDataLength));
	}

	/// <summary>
	/// Creates HTTP POST request & uploads database to server. Author : Farhan Ghumra
	/// </summary>
	public static Stream UploadFile(HttpWebRequest request, Dictionary<string, string> data,
		string fileName, string fileContentType, string filekey,
		Action<Stream> writeFileData)
    {
        string boundary = "----------" + DateTime.Now.Ticks.ToString("x");
        request.ContentType = "multipart/form-data; boundary=" + boundary;
        request.Method = "POST";
        request.Timeout = 600000;
        Stream requestStream = request.GetRequestStream();
        WriteMultipartForm(requestStream, boundary, data, fileName, fileContentType, filekey, writeFileData);
        requestStream.Flush();
        requestStream.Close();
        return request.GetResponse().GetResponseStream();
    }

    /// <summary>
    /// Writes multi part HTTP POST request. Author : Farhan Ghumra
    /// </summary>
    private static void WriteMultipartForm(Stream s, string boundary, Dictionary<string, string> data,
		string fileName, string fileContentType, string fileKey,
		Action<Stream> writeFileData)
    {
        // The first boundary
        byte[] boundarybytes = Encoding.UTF8.GetBytes("--" + boundary + "\r\n");
        // the last boundary.
        byte[] trailer = Encoding.UTF8.GetBytes("\r\n--" + boundary + "–-\r\n");
        // the form data, properly formatted
        string formdataTemplate = "Content-Disposition: form-data; name=\"{0}\"\r\n\r\n{1}";
        // the form-data file upload, properly formatted
        string fileheaderTemplate = "Content-Disposition: file; name=\"{0}\"; filename=\"{1}\";\r\nContent-Type: {2}\r\n\r\n";

        // Added to track if we need a CRLF or not.
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

        // If we don't have keys, we don't need a crlf.
        if (bNeedsCRLF)
            WriteToStream(s, "\r\n");

        WriteToStream(s, boundarybytes);
        WriteToStream(s, string.Format(fileheaderTemplate, fileKey, fileName, fileContentType));
		// Write the file data to the stream.
		writeFileData(s);
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
