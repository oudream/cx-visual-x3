using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Decision;
using CxVisual.Mechanism.Orchestration;
using CxVisual.Mechanism.Runtime.Execution;
using CxVisual.Foundation.Infrastructure.Concurrency;

namespace CxVisual.Runtime.Host
{
    /// <summary>Windows CI-only integration: real Plan+RSA envelope+Host pinned
    /// key+ledger+MRK Publisher, without external device access.</summary>
    internal static class MRHTrustedReleaseSmoke
    {
        private sealed class CiCapability : IMRKVersionedCapabilityResolver
        {
            internal int Resolutions;
            internal int Executions;
            internal int VersionChecks;
            internal string Version="vision.gray16/ci:1";
            public bool TryGetCapabilityVersion(string id,out string version)
            {
                VersionChecks++;
                version=id=="vision.gray16"?Version:null;
                return version!=null;
            }
            public bool TryResolve(string id,out MRKResolvedCapability binding)
            {
                Resolutions++;
                binding=id=="vision.gray16"?
                    new MRKResolvedCapability((ctx,token)=>{
                        Executions++;
                        return Task.FromResult((object)9);
                    },inputs:new MRKCapabilityInput[0]):null;
                return binding!=null;
            }
        }
        private sealed class CiPublicationPolicy : IMRKPlanPublicationPolicy
        {
            internal bool Safe;
            public bool Authorize(string principal,MRKPlanPublishAction action,
                MRKExecutionPlan plan,long revision)
            {return principal=="ci-release-officer";}
            public bool IsSafeToActivate(MRKExecutionPlan plan,long revision)
            {return Safe;}
        }
        private static void Check(bool ok,string why)
        {if(!ok)throw new InvalidOperationException("HOST_TRUST: "+why);}

        internal static int Run(string root)
        {
            if(string.IsNullOrWhiteSpace(root))
                throw new ArgumentException("Root required");
            Directory.CreateDirectory(root);
            const string scope="ci-signed-station";
            const string keyId="factory-key-2026";
            string certFile=Path.Combine(root,"signed-public.cer");
            string ledgerFile=Path.Combine(root,"trusted.ledger");
            string envelopeFile=Path.Combine(root,"candidate.cxsigned");

            MDCDeclarativePlan plan;
            var catalog=new MDCStaticCatalogSnapshot(new[]{
                new MOCCapabilityDescriptor("vision.gray16","Gray16","Vision")
            });
            var session=MEMEngineering.Create("host-pinned-release");
            try
            {
                var page=session.AddPage("Inspect");
                MOCFlowBuilder.Edit(session,page.RootContent,flow=>{
                    flow.Step("vision.gray16");
                });
                plan=new MDCDeclarativePlanCompiler().Compile(
                    session.GetSnapshot(),page.RootContent.Id,catalog);
            }
            finally {session.Close(true);}
            byte[] archive=MDCPlanArchiveCodec.Serialize(plan);
            var versions=new Dictionary<string,string>(StringComparer.Ordinal){
                {"vision.gray16","vision.gray16/ci:1"}
            };

            using(RSA signingKey=new RSACng(2048))
            {
                var request=new CertificateRequest("CN=CxVisual CI signing key",
                    signingKey,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
                request.CertificateExtensions.Add(new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature,true));
                using(X509Certificate2 cert=request.CreateSelfSigned(
                    DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddDays(1)))
                    File.WriteAllBytes(certFile,cert.Export(X509ContentType.Cert));
                byte[] certBytes=File.ReadAllBytes(certFile);
                string pin;
                using(SHA256 sha=SHA256.Create())
                    pin=BitConverter.ToString(sha.ComputeHash(certBytes)).Replace("-","");
                var trustedKeys=new[]{new MRHTrustedSigningKey(keyId,certFile,pin)};
                byte[] signing=MOCSignedPlanRelease.BuildSigningPayload(
                    scope,keyId,2,archive,versions);
                byte[] signature=signingKey.SignData(signing,HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1);
                var release=new MOCSignedPlanRelease(scope,keyId,2,archive,versions,signature);
                File.WriteAllBytes(envelopeFile,release.Export());

                var initial=new MRKExecutionPlan("boot",new[]{
                    new MRKExecutionStep("baseline",new string[0],
                        (ctx,token)=>Task.FromResult((object)0))
                });
                var slot=new MRKExecutionPlanSlot(initial,1,true);
                var policy=new CiPublicationPolicy();
                var publisher=new MRKExecutionPlanPublisher(slot,initial,1,policy);
                var capability=new CiCapability();
                MRHFileTrustedReleaseAdmission.CommissionNew(ledgerFile,scope,1);

                // A missing Host-commissioned journal must never be created
                // implicitly by importing an untrusted release.
                bool denied=false;
                try {new MRHTrustedReleaseHost(scope,Path.Combine(root,"missing.ledger"),
                    trustedKeys,p=>p=="ci-release-officer",publisher,catalog,capability);}
                catch(FileNotFoundException) {denied=true;}
                Check(denied,"missing durable watermark was silently reset");

                var wrongPin=new[]{new MRHTrustedSigningKey(keyId,certFile,
                    new string('0',64))};
                denied=false;
                try {new MRHTrustedReleaseHost(scope,ledgerFile,wrongPin,
                    p=>p=="ci-release-officer",publisher,catalog,capability);}
                catch(UnauthorizedAccessException e)
                {denied=e.Message=="mrh.release.certificate_pin_mismatch";}
                Check(denied,"untrusted public certificate pin was accepted");

                // A pinned RSA certificate with only key-encipherment use is
                // not a valid signing identity, regardless of its fingerprint.
                string badUsageFile=Path.Combine(root,"not-for-signing.cer");
                var badUsageRequest=new CertificateRequest(
                    "CN=CxVisual CI encipherment-only key",
                    signingKey,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
                badUsageRequest.CertificateExtensions.Add(new X509KeyUsageExtension(
                    X509KeyUsageFlags.KeyEncipherment,true));
                using(X509Certificate2 badUsageCert=badUsageRequest.CreateSelfSigned(
                    DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddDays(1)))
                    File.WriteAllBytes(badUsageFile,badUsageCert.Export(X509ContentType.Cert));
                string badUsagePin;
                using(SHA256 sha=SHA256.Create())
                    badUsagePin=BitConverter.ToString(
                        sha.ComputeHash(File.ReadAllBytes(badUsageFile))).Replace("-","");
                denied=false;
                try {new MRHTrustedReleaseHost(scope,ledgerFile,
                    new[]{new MRHTrustedSigningKey(keyId,badUsageFile,badUsagePin)},
                    p=>p=="ci-release-officer",publisher,catalog,capability);}
                catch(UnauthorizedAccessException e)
                {denied=e.Message=="mrh.release.certificate_not_signing";}
                Check(denied,"non-signing certificate was accepted as trusted signing key");

                var trusted=new MRHTrustedReleaseHost(scope,ledgerFile,trustedKeys,
                    p=>p=="ci-release-officer",publisher,catalog,capability);

                byte[] tampered=File.ReadAllBytes(envelopeFile);
                tampered[tampered.Length-1]^=1;
                File.WriteAllBytes(envelopeFile,tampered);
                denied=false;
                try {trusted.StageFile("ci-release-officer",envelopeFile);}
                catch(UnauthorizedAccessException e)
                {denied=e.Message=="moc.release.signature_invalid";}
                Check(denied&&capability.Resolutions==0&&
                    !publisher.PendingRevision.HasValue,
                    "invalid signed file touched MRK or professional resolver");
                File.WriteAllBytes(envelopeFile,release.Export());
                denied=false;
                try {trusted.StageFile("readonly",envelopeFile);}
                catch(UnauthorizedAccessException) {denied=true;}
                Check(denied&&!publisher.PendingRevision.HasValue,
                    "unauthorized Host principal staged a plan");
                MRKPlanPublicationReceipt receipt=trusted.StageFile(
                    "ci-release-officer",envelopeFile);
                Check(receipt.Accepted&&receipt.Revision==2&&
                    publisher.PendingRevision==2&&capability.Resolutions==1&&
                    capability.Executions==0,
                    "real Host pinned certificate and signed Plan failed to Stage");
                int beforeUnauthorized=capability.VersionChecks;
                denied=false;
                try {trusted.TryActivate("readonly");}
                catch(UnauthorizedAccessException e)
                {denied=e.Message=="mrh.release.activation_principal_denied";}
                Check(denied && capability.VersionChecks==beforeUnauthorized &&
                    publisher.ActiveRevision==1 && publisher.PendingRevision==2,
                    "unprivileged activation must not query the professional Resolver");
                capability.Version="vision.gray16/ci:2";
                denied=false;
                try {trusted.TryActivate("ci-release-officer");}
                catch(InvalidOperationException e)
                {denied=e.Message.StartsWith(
                    "mrh.release.provider_version_changed:",StringComparison.Ordinal);}
                Check(denied && publisher.ActiveRevision==1 &&
                    publisher.PendingRevision==2 && capability.Executions==0,
                    "professionally hot-swapped algorithm between Stage and Activate must fail closed");
                capability.Version="vision.gray16/ci:1";
                Check(!trusted.TryActivate("ci-release-officer")&&
                    publisher.ActiveRevision==1,
                    "release signature bypassed the real MRK Host safe point");
                policy.Safe=true;
                Check(trusted.TryActivate("ci-release-officer")&&
                    publisher.ActiveRevision==2,
                    "real MRK Publisher failed safe activation");
                // B6: Stage and Activate alone do not prove the signed
                // archive can actually execute. Capture the live MRK PlanSlot
                // lease and run its original provider delegate on the genuine
                // F19-backed execution engine; no separate Host scheduler.
                using(MRKExecutionPlanLease lease=slot.Capture())
                {
                    Check(lease.Plan.Id!="boot"&&lease.Revision==2,
                        "MRK slot did not retain compiled signed Plan");
                    var engine=new MRKExecutionEngine(new FCRWorkerOptions(2,16));
                    try
                    {
                        MRKFlowHandle flow=engine.StartFlow(lease.Plan);
                        MRKFlowResult terminal=flow.Completion.GetAwaiter().GetResult();
                        Check(terminal.State==MRKFlowState.Completed&&
                            terminal.FlowId==flow.FlowId&&
                            terminal.Results.Count==1&&
                            terminal.Results.Values.Any(x=>x is int&&(int)x==9)&&
                            capability.Executions==1,
                            "B6 signed and activated MRK Plan did not execute the exact trusted provider");
                    }
                    finally {engine.StopAsync(true).GetAwaiter().GetResult();}
                }

                // New Host instance, process-equivalent reopen: the already
                // reserved revision is still rejected by the file ledger.
                var nextSlot=new MRKExecutionPlanSlot(initial,1,true);
                var nextPublisher=new MRKExecutionPlanPublisher(nextSlot,initial,1,
                    new CiPublicationPolicy{Safe=true});
                var restarted=new MRHTrustedReleaseHost(scope,ledgerFile,trustedKeys,
                    p=>p=="ci-release-officer",nextPublisher,catalog,
                    new CiCapability());
                denied=false;
                try {restarted.StageFile("ci-release-officer",envelopeFile);}
                catch(UnauthorizedAccessException e)
                {denied=e.Message=="moc.release.admission_denied";}
                Check(denied&&!nextPublisher.PendingRevision.HasValue,
                    "restarted Host accepted replayed signed revision");
                using(var stream=new FileStream(ledgerFile,FileMode.Append,
                    FileAccess.Write,FileShare.None))
                    stream.WriteByte(0x42);
                denied=false;
                try {new MRHTrustedReleaseHost(scope,ledgerFile,trustedKeys,
                    p=>p=="ci-release-officer",nextPublisher,catalog,
                    new CiCapability());}
                catch(InvalidDataException e)
                {denied=e.Message=="mrh.release.torn_record";}
                Check(denied,"damaged durable ledger was accepted on restart");
            }
            Console.WriteLine("PASS trusted Host: certificate pin, signed Plan, real MRK Stage/Activate, restarted replay and ledger tamper denial");
            return 0;
        }
    }
}
