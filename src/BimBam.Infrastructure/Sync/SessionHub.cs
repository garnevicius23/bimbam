using BimBam.Core.Models;
using BimBam.Infrastructure.Sync.Models;
using Microsoft.AspNetCore.SignalR;

namespace BimBam.Infrastructure.Sync;

/// <summary>SignalR endpoint other laptops talk to. All real work is in <see cref="SessionHostServer"/>.</summary>
public sealed class SessionHub(SessionHostServer host) : Hub
{
    public override async Task OnConnectedAsync()
    {
        if (!LocalNetworkAddress.IsAllowed(Context.GetHttpContext()?.Connection.RemoteIpAddress))
        {
            Context.Abort();
            return;
        }

        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        host.RemoveClient(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    public HostAnnouncement Hello() => host.Announcement;

    public async Task<JoinResponse> Join(JoinRequest request)
    {
        // Join the broadcast group before the snapshot is taken, so no operation applied in
        // between can be missed; a duplicate is harmless because operations are deduplicated.
        await Groups.AddToGroupAsync(Context.ConnectionId, SyncProtocol.JoinedGroup);
        var response = await host.JoinAsync(Context.ConnectionId, request);
        if (!response.Accepted)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, SyncProtocol.JoinedGroup);
        }

        return response;
    }

    /// <returns>IDs of the operations the host now has (the client's acknowledgement).</returns>
    public Task<List<Guid>> Submit(List<SessionOperation> operations) =>
        host.SubmitAsync(Context.ConnectionId, operations);
}
