using System;
using System.Threading.Tasks;
using CxVisual.Mechanism.Runtime.Commands;

namespace CxVisual.Runtime.Host
{
    /// <summary>
    /// Explicit, Host-owned SCADA control composition. No separate command bus,
    /// driver or authorization store: uses the sole MRK center, its Windows
    /// authenticated IPC, and the already-pinned PAD device registration.
    /// This type does NOT enable any control CLI or make OS account equality
    /// equivalent to a production operator role/safety interlock.
    /// </summary>
    internal sealed class MRHScadaTrustedCommandPipe : IDisposable
    {
        private readonly MRHScadaRuntime _device;
        private readonly Func<string,bool> _authorizePeer;
        private readonly Func<MRKCommandContext,bool> _authorizeCommand;
        private readonly MRKCommandCenter _commands;
        private readonly MRKCommandPipeServer _server;
        private readonly IDisposable _registration;
        private bool _started;
        private bool _disposed;

        /// <param name="authorizePeer">Trusted Host policy for actual OS peer account.
        /// Must not trust wire Caller or an HMI-supplied name.</param>
        /// <param name="authorizeCommand">Trusted Host principal/role/mode/physical
        /// guard policy; the PAD provider independently checks point-level access.</param>
        internal MRHScadaTrustedCommandPipe(MRHScadaRuntime device,
            string pipeName, long commandId,
            Func<string,bool> authorizePeer,
            Func<MRKCommandContext,bool> authorizeCommand)
        {
            if(device==null)throw new ArgumentNullException("device");
            if(authorizePeer==null || authorizeCommand==null)
                throw new UnauthorizedAccessException(
                    "scada.command.explicit_peer_and_operation_policy_required");
            if(!device.IsStillApproved)
                throw new InvalidOperationException(
                    "scada.command.deployment_not_approved");
            _device=device;
            _authorizePeer=authorizePeer;
            _authorizeCommand=authorizeCommand;
            _commands=new MRKCommandCenter(authorize:context=>
                _device.IsRunning && _device.IsStillApproved &&
                _authorizeCommand(context));
            try
            {
                // PAD requires a trusted point authorization delegate and
                // register-before-Start. Default read-only SCADA fails closed.
                _registration=_device.RegisterTrustedWriteCommands(_commands,commandId);
                _server=new MRKCommandPipeServer(_commands,pipeName,peer=>
                    _device.IsRunning && _device.IsStillApproved &&
                    _authorizePeer(peer));
            }
            catch
            {
                if(_registration!=null)_registration.Dispose();
                _commands.Dispose();
                throw;
            }
        }

        internal MRKCommandCenter Commands {get{return _commands;}}
        internal void Start()
        {
            if(_disposed)throw new ObjectDisposedException(
                "MRHScadaTrustedCommandPipe");
            if(_started)throw new InvalidOperationException(
                "SCADA命令Pipe已启动");
            if(!_device.IsRunning || !_device.IsStillApproved)
                throw new InvalidOperationException(
                    "scada.command.session_not_running_or_approved");
            _server.Start();
            _started=true;
        }

        internal async Task StopAsync()
        {
            if(_disposed)return;
            _disposed=true;
            try
            {
                if(_server!=null)
                    await _server.StopAsync().ConfigureAwait(false);
            }
            finally
            {
                _registration.Dispose();
                _commands.Dispose();
            }
        }

        public void Dispose()
        {
            StopAsync().GetAwaiter().GetResult();
        }
    }
}
