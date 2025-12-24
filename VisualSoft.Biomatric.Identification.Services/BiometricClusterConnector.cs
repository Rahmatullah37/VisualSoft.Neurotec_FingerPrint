using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Neurotec.Biometrics;
using Neurotec.Biometrics.Client;
using Neurotec.Images;
using Neurotec.Licensing;
using Serilog.Filters;
using VisualSoft.Biomatric.Identification.Domain.Models;

namespace VisualSoft.Biomatric.Identification.Services
{
    public class BiometricClusterConnector : IBiometricService
    {
        private readonly NBiometricClient _client;
        private readonly ILogger<BiometricClusterConnector> _logger;
        private bool _disposed = false;

        private BiometricClusterConnector(NBiometricClient client, ILogger<BiometricClusterConnector> logger)
        {
            _client = client;
            _logger = logger;
        }

        public static async Task<BiometricClusterConnector> ConnectAsync(
            IConfiguration config,
            ILogger<BiometricClusterConnector> logger)
        {
            try
            {
                logger.LogInformation("=== Initializing Biometric Cluster Connector ===");

                var neurotecSection = config.GetSection("Neurotec");

                string clusterServer = neurotecSection["ClusterServer"];
                int clientPort = int.Parse(neurotecSection["ClientPort"]);
                int adminPort = int.Parse(neurotecSection["AdminPort"]);
                string licenseServer = neurotecSection["LicenseServer"];
                string licensePort = neurotecSection["LicensePort"];
                string[] licenses = neurotecSection.GetSection("Licenses").Get<string[]>();

                logger.LogInformation("Cluster Server: {ClusterServer}:{ClientPort}/{AdminPort}",
                    clusterServer, clientPort, adminPort);
                logger.LogInformation("License Server: {LicenseServer}:{LicensePort}",
                    licenseServer, licensePort);

                // Setup native DLL paths
                string baseDir = AppContext.BaseDirectory;
                Environment.SetEnvironmentVariable("PATH", baseDir + ";" + Environment.GetEnvironmentVariable("PATH"));
                logger.LogDebug("Native DLL path configured: {BaseDir}", baseDir);

                // Obtain licenses
                logger.LogInformation("Obtaining Neurotec licenses...");
                foreach (var license in licenses)
                {
                    logger.LogDebug("  - Obtaining license: {License}", license);
                    if (!NLicense.ObtainComponents(licenseServer, licensePort, license))
                    {
                        logger.LogError("Failed to obtain Neurotec license: {License}", license);
                        throw new Exception($"Failed to obtain Neurotec license: {license}");
                    }
                }
                logger.LogInformation("All licenses obtained successfully");

                // Create and configure NBiometricClient
                var client = new NBiometricClient
                {
                    UseDeviceManager = false,
                    BiometricTypes = NBiometricType.Finger,
                    MatchingThreshold = 80,
                    FingersFastExtraction = true,
                    FingersReturnBinarizedImage = true,
                    FingersCalculateNfiq = true
                };

                logger.LogDebug("NBiometricClient Configuration:");
                logger.LogDebug("  - MatchingThreshold: {Threshold}", client.MatchingThreshold);
                logger.LogDebug("  - QualityThreshold: {QualityThreshold}", client.FingersQualityThreshold);
                logger.LogDebug("  - FastExtraction: {FastExtraction}", client.FingersFastExtraction);

                // Setup remote cluster connection
                client.RemoteConnections.Clear();
                client.RemoteConnections.Add(new NClusterBiometricConnection(clusterServer, clientPort, adminPort));

                logger.LogInformation("Initializing connection to cluster...");
                await client.InitializeAsync();

                // Configure local operations
                client.LocalOperations = NBiometricOperations.CreateTemplate
                                       | NBiometricOperations.Detect
                                       | NBiometricOperations.DetectSegments
                                       | NBiometricOperations.Segment
                                       | NBiometricOperations.AssessQuality;

                logger.LogInformation(" Successfully connected to Biometric Cluster");
                logger.LogInformation("=== Biometric Service Ready ===");

                return new BiometricClusterConnector(client, logger);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to initialize Biometric Cluster Connector");
                throw;
            }
        }

        public async Task<IdentificationResult> IdentifyAsync(string wsqFilePath)
        {
            var startTime = DateTime.UtcNow;
            _logger.LogInformation("Starting identification for file: {FilePath}", wsqFilePath);

            NSubject subject = null;

            try
            {
                // Validate file exists
                if (!File.Exists(wsqFilePath))
                {
                    _logger.LogWarning("WSQ file not found: {FilePath}", wsqFilePath);
                    return new IdentificationResult
                    {
                        Success = false,
                        Status = "FileNotFound",
                        Message = "WSQ file not found"
                    };
                }

                // Create subject and add finger
                subject = new NSubject();
                var finger = new NFinger
                {
                    Image = NImage.FromFile(wsqFilePath),
                    Position = NFPosition.RightIndex,
                    ImpressionType = NFImpressionType.NonliveScanPlain
                };
                subject.Fingers.Add(finger);

                // Step 1: Extract template
                _logger.LogDebug("Step 1: Extracting fingerprint template...");
                var extractTask = _client.CreateTask(NBiometricOperations.CreateTemplate, subject);
                extractTask = await _client.PerformTaskAsync(extractTask);

                if (extractTask.Status != NBiometricStatus.Ok)
                {
                    _logger.LogWarning("Template extraction failed with status: {Status}", extractTask.Status);
                    return new IdentificationResult
                    {
                        Success = false,
                        Status = extractTask.Status.ToString(),
                        Message = $"Template extraction failed: {extractTask.Status}"
                    };
                }

                _logger.LogDebug(" Template extracted successfully");

                //  Perform identification
                _logger.LogDebug("Performing identification against database...");
                var identifyTask = _client.CreateTask(NBiometricOperations.Identify, subject);
                identifyTask = await _client.PerformTaskAsync(identifyTask);

                var result = new IdentificationResult
                {
                    Success = identifyTask.Status == NBiometricStatus.Ok,
                    Status = identifyTask.Status.ToString(),
                    MatchingThreshold = _client.MatchingThreshold,
                    Message = identifyTask.Status == NBiometricStatus.Ok
                        ? "Identification completed successfully"
                        : $"Identification failed: {identifyTask.Status}"
                };

                // Check for matches
                if (subject.MatchingResults != null && subject.MatchingResults.Count > 0)
                {
                    // Best match
                    var bestMatch = subject.MatchingResults[0];
                    result.MatchedSubjectId = bestMatch.Id;
                    result.MatchingScore = bestMatch.Score;
                    result.TotalMatches = subject.MatchingResults.Count;

                    // All matches
                    result.AllMatches = subject.MatchingResults
                        .Select(m => new MatchInfo
                        {
                            SubjectId = m.Id,
                            Score = m.Score
                        })
                        .ToList();

                    var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;

                    _logger.LogInformation(
                        " Match found - Best: {SubjectId} (Score: {Score}), Total Matches: {TotalMatches}, Duration: {Duration}ms",
                        bestMatch.Id, bestMatch.Score, result.TotalMatches, duration);

                    if (subject.MatchingResults.Count > 1)
                    {
                        _logger.LogDebug("All matches: {Matches}",
                            string.Join(", ", subject.MatchingResults.Select(m => $"{m.Id}({m.Score})")));
                    }
                }
                else
                {
                    result.TotalMatches = 0;
                    result.AllMatches = new List<MatchInfo>();

                    var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
                    _logger.LogInformation(
                        " No match found - Status: {Status}, Duration: {Duration}ms",
                        identifyTask.Status, duration);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during identification for file: {FilePath}", wsqFilePath);
                return new IdentificationResult
                {
                    Success = false,
                    Status = "Error",
                    Message = $"Error: {ex.Message}",
                    AllMatches = new List<MatchInfo>(),
                    TotalMatches = 0
                };
            }
            finally
            {
                subject?.Dispose();
            }
        }
 public void Dispose()
        {
            if (!_disposed)
            {
                _logger.LogInformation("Disposing BiometricClusterConnector");
                _client?.Dispose();
                _disposed = true;
            }
        }
    }
}