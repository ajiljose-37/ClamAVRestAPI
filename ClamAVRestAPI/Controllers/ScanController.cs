using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Threading.Tasks;
using Docker.DotNet;
using Docker.DotNet.Models;
using System;
using System.Collections.Generic;

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

            //var result = await ScanWithClamAV(uploadPath);
            var result = await ScanTest(uploadPath);

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

        private async Task<string> ScanTest(string filepath)
        {

            try
            {
                var clam = new ClamClient("localhost", 7214);
                var pingResult = await clam.TryPingAsync();

                if (!pingResult)
                {
                    Console.WriteLine("test failed. Exiting.");
                    return null;
                }

                Console.WriteLine("connected.");

                Console.Write("\t* Scanning file: ");
                var scanResult = await clam.ScanFileOnServerAsync(filepath);  //any file you would like!

                switch (scanResult.Result)
                {
                    case ClamScanResults.Clean:
                        Console.WriteLine("The file is clean!");
                        break;
                    case ClamScanResults.VirusDetected:
                        Console.WriteLine("Virus Found!");
                        Console.WriteLine("Virus name: {0}", scanResult.InfectedFiles.First().VirusName);
                        break;
                    case ClamScanResults.Error:
                        Console.WriteLine("Woah an error occured! Error: {0}", scanResult.RawResult);
                        break;
                }
                return scanResult.RawResult;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return ex.Message;
            }
        }
    }
}
