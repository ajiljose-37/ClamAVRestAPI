using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Threading.Tasks;
using Docker.DotNet;
using Docker.DotNet.Models;
using System;
using System.Collections.Generic;
using nClam;

namespace ClamAVRestAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ScanController : ControllerBase
    {
        private readonly IDockerClient _dockerClient;

        public ScanController()
        {
            _dockerClient = new DockerClientConfiguration(new Uri("npipe://./pipe/docker_engine")).CreateClient();
        }

        [HttpPost("scan")]
        public async Task<IActionResult> ScanFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { error = "No file provided" });
            }

            var uploadPath = Path.Combine("D:\\Learning Section\\ClamAVRestAPI\\ClamAVRestAPI\\tempFile\\", file.FileName);
            //var uploadPath = "C:\\Users\\ajil.jose\\Downloads\\UK_FORM_PRAKASH.pdf";

            using (var stream = new FileStream(uploadPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var result = await ScanFiles(uploadPath);

            System.IO.File.Delete(uploadPath); // Clean up the file after scanning

            return Ok(result);
        }

        private async Task<string> ScanWithClamAV(string filePath)
        {
            try
            {
                var fileName = Path.GetFileName(filePath);

                var createParameters = new CreateContainerParameters
                {
                    Image = "clamav/clamav:latest",
                    HostConfig = new HostConfig
                    {
                        Binds = new List<string> { $"{filePath}:/scan/{fileName}" }
                    },
                    Cmd = new List<string> { "clamscan", $"/scan/{fileName}" }
                };

                var containerCreateResponse = await _dockerClient.Containers.CreateContainerAsync(createParameters);

                if (!await _dockerClient.Containers.StartContainerAsync(containerCreateResponse.ID, null))
                {
                    throw new Exception("Failed to start container.");
                }

                var logParameters = new ContainerLogsParameters
                {
                    ShowStdout = true,
                    ShowStderr = true,
                    Follow = true
                };
                //docker run -d -p 8080:80 --name <mynginx> nginx

                using (var logsStream = await _dockerClient.Containers.GetContainerLogsAsync(containerCreateResponse.ID, logParameters))
                {
                    using (var reader = new StreamReader(logsStream))
                    {
                        var output = await reader.ReadToEndAsync();

                        await _dockerClient.Containers.RemoveContainerAsync(containerCreateResponse.ID, new ContainerRemoveParameters { Force = true });

                        return output;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return ex.Message;
            }
        }

        public async Task<string> ScanFiles(string filePath)
        {
            // Specify the ClamAV Daemon IP and Port (localhost:3310)
            var clam = new ClamClient("localhost", 3310);

            string result = string.Empty;

            // File to scan (Ensure the file path is accessible)
            //string filePath = "path/to/file";

            // Read the file bytes
            byte[] fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);

            // Scan the file using ClamAV
            var scanResult = await clam.SendAndScanFileAsync(fileBytes);

            // Evaluate the result
            if (scanResult.Result == ClamScanResults.Clean)
            {
                result = "The file is clean!";
            }
            else if (scanResult.Result == ClamScanResults.VirusDetected)
            {
                result = $"Virus detected: {scanResult.InfectedFiles[0].VirusName}";
            }
            else
            {
                result = "Error scanning the file.";
            }

            return result;
        }

    }
}
