using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SiteSyncTracker.Workspace;
using Xunit;

namespace SiteSyncTracker.Web.Tests;

public sealed class WorkspaceTests
{
    private static async Task<HttpClient> ClientAsync(TestApplicationFactory factory)
    {
        var client=factory.CreateClient(new WebApplicationFactoryClientOptions {AllowAutoRedirect=false});
        var email=$"client-{Guid.NewGuid():N}@example.com";
        const string password="Strong-Client-Password-123!";
        using var registration=await client.PostAsJsonAsync("/api/auth/register",new {email,password});
        Assert.Equal(HttpStatusCode.Created,registration.StatusCode);
        using var login=await client.PostAsJsonAsync("/login",new {email,password});
        Assert.Equal(HttpStatusCode.OK,login.StatusCode);
        var response=await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",response.GetProperty("accessToken").GetString());
        return client;
    }
    [Fact]
    public async Task Client_can_add_own_stock_without_exposing_it_to_another_client()
    {
        using var factory=new TestApplicationFactory();
        using var alice=await ClientAsync(factory);
        using var bob=await ClientAsync(factory);
        var document=(await alice.GetFromJsonAsync<WorkspaceDocument>("/api/workspace"))!;
        Assert.Equal(72,document.Items.Count);
        document.Items.Add(new StockItem {Sku="PRIVATE-ALICE",Name="Client custom stock",Category="Last minute",Unit="pcs",OpeningQuantity=10});
        using var save=await alice.PutAsJsonAsync("/api/workspace",document);
        Assert.Equal(HttpStatusCode.OK,save.StatusCode);
        var saved=(await alice.GetFromJsonAsync<WorkspaceDocument>("/api/workspace"))!;
        Assert.Contains(saved.Items,i=>i.Sku=="PRIVATE-ALICE");
        var other=(await bob.GetFromJsonAsync<WorkspaceDocument>("/api/workspace"))!;
        Assert.DoesNotContain(other.Items,i=>i.Sku=="PRIVATE-ALICE");
    }
    [Fact]
    public async Task Concurrent_update_is_rejected_without_overwriting_saved_stock()
    {
        using var factory=new TestApplicationFactory();using var client=await ClientAsync(factory);
        var document=(await client.GetFromJsonAsync<WorkspaceDocument>("/api/workspace"))!;
        using var first=await client.PutAsJsonAsync("/api/workspace",document);
        Assert.Equal(HttpStatusCode.OK,first.StatusCode);
        document.Items[0].Name="Stale change";
        using var stale=await client.PutAsJsonAsync("/api/workspace",document);
        Assert.Equal(HttpStatusCode.Conflict,stale.StatusCode);
        var current=(await client.GetFromJsonAsync<WorkspaceDocument>("/api/workspace"))!;
        Assert.NotEqual("Stale change",current.Items[0].Name);
    }
    [Fact]
    public async Task Signed_record_cannot_be_edited_or_used_to_issue_unavailable_stock()
    {
        using var factory=new TestApplicationFactory();using var client=await ClientAsync(factory);
        var document=(await client.GetFromJsonAsync<WorkspaceDocument>("/api/workspace"))!;
        document.Movements.Add(new GoodsMovement {Id="receipt-1",Sku=document.Items[0].Sku,Kind="receive",Quantity=4,Reference="JOB-42",Condition="accepted",Signer="Test receiver",Location="Test yard",OccurredAt=DateTimeOffset.UtcNow,RecordedAt=DateTimeOffset.UtcNow,Acknowledged=true,Signature="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAKAAAAAyCAIAAABUA0cyAAAByUlEQVR4nO2ay3LDIBAERSr//8vKIRFR2UgC9sl4+uBL7GiZrl0ZrLLv+0Zw+YougNhCweBQMDgUDA4Fg0PB4FAwOBQMDgX/U0oppURXoQwF/1HVgmmOEfybYJIcm0bLQUhJihTns+j3yGIPw5v1ZCtSgqvgZkNEZddj8eU9K2p2Enw/6/yDG+rRpTWbC77q2sDU5i696Ny2FXwfyvmvUYNk9LrLabYS3BmEp2NdN6vMbX3BQ9+kfGKya7v8mpUFT0Rp3cQOQzXz3FYTLFmknWPPDsupWUGwfHdroSEw7lRzWypYK0fdJs7QTBlq2CSC1Reg4jhJrJXwer4nPpPqxPFMeJrv1ALOv1ZtnveLVDt9SROnuvNd4V/kgGD//QbqIaJnwV2CPWfyaBMvZ7fiU/mzYP8EOx2vq/YF07l9JzgqwZ4Fw9itGGl+6GD/H3x6rrvE96k59DefPSM6ZofecozXuE0Ul/n80F2GBK8e0stQmwX7gfxfxXRnJzcP+mQuWx3JEE39XPTVqj7K7iZb78xRZSCfplZO6g7eDqPnVzJE6nswkZO9g4kQCgaHgsGhYHAoGBwKBoeCwaFgcCgYHAoGh4LBoWBwfgDSLVMmmOXUeQAAAABJRU5ErkJggg=="});
        using var first=await client.PutAsJsonAsync("/api/workspace",document);
        Assert.Equal(HttpStatusCode.OK,first.StatusCode);
        var saved=(await first.Content.ReadFromJsonAsync<WorkspaceDocument>())!;
        saved.Movements[0].Quantity=5;
        using var changed=await client.PutAsJsonAsync("/api/workspace",saved);
        Assert.Equal(HttpStatusCode.BadRequest,changed.StatusCode);
        saved=(await client.GetFromJsonAsync<WorkspaceDocument>("/api/workspace"))!;
        saved.Movements.Add(new GoodsMovement {Id="issue-2",Sku=saved.Items[0].Sku,Kind="issue",Quantity=10,Reference="JOB-42",Condition="accepted",Signer="Test issuer",Location="Test yard",OccurredAt=DateTimeOffset.UtcNow,RecordedAt=DateTimeOffset.UtcNow,Acknowledged=true,Signature="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAKAAAAAyCAIAAABUA0cyAAAByUlEQVR4nO2ay3LDIBAERSr//8vKIRFR2UgC9sl4+uBL7GiZrl0ZrLLv+0Zw+YougNhCweBQMDgUDA4Fg0PB4FAwOBQMDgX/U0oppURXoQwF/1HVgmmOEfybYJIcm0bLQUhJihTns+j3yGIPw5v1ZCtSgqvgZkNEZddj8eU9K2p2Enw/6/yDG+rRpTWbC77q2sDU5i696Ny2FXwfyvmvUYNk9LrLabYS3BmEp2NdN6vMbX3BQ9+kfGKya7v8mpUFT0Rp3cQOQzXz3FYTLFmknWPPDsupWUGwfHdroSEw7lRzWypYK0fdJs7QTBlq2CSC1Reg4jhJrJXwer4nPpPqxPFMeJrv1ALOv1ZtnveLVDt9SROnuvNd4V/kgGD//QbqIaJnwV2CPWfyaBMvZ7fiU/mzYP8EOx2vq/YF07l9JzgqwZ4Fw9itGGl+6GD/H3x6rrvE96k59DefPSM6ZofecozXuE0Ul/n80F2GBK8e0stQmwX7gfxfxXRnJzcP+mQuWx3JEE39XPTVqj7K7iZb78xRZSCfplZO6g7eDqPnVzJE6nswkZO9g4kQCgaHgsGhYHAoGBwKBoeCwaFgcCgYHAoGh4LBoWBwfgDSLVMmmOXUeQAAAABJRU5ErkJggg=="});
        using var negative=await client.PutAsJsonAsync("/api/workspace",saved);
        Assert.Equal(HttpStatusCode.BadRequest,negative.StatusCode);
    }
    [Fact]
    public async Task Workspace_requires_sign_in_and_ui_assets_are_packaged()
    {
        using var factory=new TestApplicationFactory();using var client=factory.CreateClient();
        using var response=await client.GetAsync("/api/workspace");
        Assert.Equal(HttpStatusCode.Unauthorized,response.StatusCode);
        using var asset=await client.GetAsync("/workspace/core.js");
        Assert.Equal(HttpStatusCode.OK,asset.StatusCode);
        Assert.Contains("Shared offline inventory rules",await asset.Content.ReadAsStringAsync());
    }
}
