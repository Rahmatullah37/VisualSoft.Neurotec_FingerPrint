//using Microsoft.Extensions.Configuration;
//using Neurotec.Accelerator.Admin.Rest.Client;
//using Neurotec.Biometrics;
//using Neurotec.Biometrics.Client;
//using Neurotec.Licensing;
//using VisualSoft.Biomatric.Identification.Domain.Models;
//using System;
//using System.Threading.Tasks;

//namespace VisualSoft.Biomatric.Identification.Services
//{
//    public class BiometricCluster : IDisposable
//    {
//        private readonly NBiometricClient _client;

//        private BiometricCluster(NBiometricClient client)
//        {
//            _client = client;
//        }

//        public static async Task<BiometricClusterConnector> ConnectAsync(IConfiguration config)
//        {
//            var neurotecSection = config.GetSection("Neurotec");
//            string clusterServer = neurotecSection["ClusterServer"];
//            int clientPort = int.Parse(neurotecSection["ClientPort"]);
//            int adminPort = int.Parse(neurotecSection["AdminPort"]);
//            string licenseServer = neurotecSection["LicenseServer"];
//            int licensePort = int.Parse(neurotecSection["LicensePort"]);
//            string[] licenses = neurotecSection.GetSection("Licenses").Get<string[]>();

//            // Setup native DLL paths
//            string baseDir = AppContext.BaseDirectory;
//            Environment.SetEnvironmentVariable("PATH", baseDir + ";" + Environment.GetEnvironmentVariable("PATH"));

//            // Obtain licenses
//            foreach (var license in licenses)
//            {
//                if (!NLicense.ObtainComponents(licenseServer, licensePort, license))
//                {
//                    throw new Exception($"Failed to obtain Neurotec license: {license}");
//                }
//            }

//            var client = new NBiometricClient
//            {
//                UseDeviceManager = false,
//                BiometricTypes = NBiometricType.Finger,
//                MatchingThreshold = 48,
//                FingersFastExtraction = true,
//                FingersReturnBinarizedImage = true,
//                FingersCalculateNfiq = true,
//                FingersQualityThreshold = 35
//            };

//            client.RemoteConnections.Clear();
//            client.RemoteConnections.Add(new NClusterBiometricConnection(clusterServer, clientPort, adminPort));

//            await client.InitializeAsync();

//            client.LocalOperations = NBiometricOperations.CreateTemplate
//                                   | NBiometricOperations.Detect
//                                   | NBiometricOperations.DetectSegments
//                                   | NBiometricOperations.Segment
//                                   | NBiometricOperations.AssessQuality;

//            return new BiometricCluster(client);
//        }

//        public async Task<IdentificationResult> IdentifyAsync(string wsqFilePath)
//        {
//            var subject = new NSubject();
//            subject.Fingers.Add(new NFinger
//            {
//                Image = Neurotec.Images.NImage.FromFile(wsqFilePath),
//                Position = NFPosition.RightIndex,
//                ImpressionType = NFImpressionType.NonliveScanPlain
//            });

//            var extractTask = _client.CreateTask(NBiometricOperations.CreateTemplate, subject);
//            extractTask = await _client.PerformTaskAsync(extractTask);

//            if (extractTask.Status != NBiometricStatus.Ok)
//                return new IdentificationResult { Success = false, Message = "Template extraction failed" };

//            var identifyTask = _client.CreateTask(NBiometricOperations.Identify, subject);
//            identifyTask = await _client.PerformTaskAsync(identifyTask);

//            var result = new IdentificationResult
//            {
//                Success = identifyTask.Status == NBiometricStatus.Ok,
//                Status = identifyTask.Status.ToString(),
//                MatchingThreshold = _client.MatchingThreshold,
//                Message = identifyTask.Status == NBiometricStatus.Ok ? "Identification done" : "Identification failed"
//            };

//            if (subject.MatchingResults.Count > 0)
//            {
//                var best = subject.MatchingResults[0];
//                result.MatchedSubjectId = best.Id;
//                result.MatchingScore = best.Score;
//            }

//            subject.Dispose();
//            return result;
//        }

//        public void Dispose()
//        {
//            _client?.Dispose();
//        }
//    }
//}
