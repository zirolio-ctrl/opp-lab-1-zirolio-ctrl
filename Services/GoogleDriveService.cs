using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Google.Apis.Upload;
using Google.Apis.Download;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace opp_lab_1_zirolio_ctrl.Models;

public sealed class GoogleDriveService
{
    private readonly DriveService _drive;

    public GoogleDriveService()
    {
        using var stream = new FileStream(
            "client_secrets.json",
            FileMode.Open,
            FileAccess.Read);

        var secrets = GoogleClientSecrets
            .FromStream(stream)
            .Secrets;

        var credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
            secrets,
            [DriveService.Scope.DriveFile],
            "user",
            CancellationToken.None,
            new FileDataStore("token.json", true))
            .GetAwaiter()
            .GetResult();

        _drive = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "Table Editor"
        });
    }

    public async Task<string> UploadJsonAsync(
        string fileName,
        string json)
    {
        var metadata = new Google.Apis.Drive.v3.Data.File
        {
            Name = fileName,
            MimeType = "application/json"
        };

        using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes(json));

        var request = _drive.Files.Create(
            metadata,
            stream,
            "application/json"
        );

        request.Fields = "id,name";
        await request.UploadAsync();

        if (request.ResponseBody is null)
            throw new IOException("Не удалось загрузить файл на Google Drive.");

        return request.ResponseBody.Id;
    }

    public async Task<List<Google.Apis.Drive.v3.Data.File>>
        GetJsonFilesAsync()
    {
        var request = _drive.Files.List();

        request.Q = "trashed = false and mimeType = 'application/json'";
        request.Fields = "files(id,name,modifiedTime)";
        request.OrderBy = "modifiedTime desc";

        var response = await request.ExecuteAsync();

        return response.Files?.ToList() ?? new List<Google.Apis.Drive.v3.Data.File>();
    }

    public async Task<string> DownloadJsonAsync(string fileId)
    {
        var request = _drive.Files.Get(fileId);

        using var stream = new MemoryStream();

        await request.DownloadAsync(stream);

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}