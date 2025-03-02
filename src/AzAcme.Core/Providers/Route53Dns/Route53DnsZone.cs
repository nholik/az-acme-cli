using Amazon;
using Amazon.Route53;
using Amazon.Route53.Model;
using Amazon.Runtime;
using AzAcme.Core.Providers.Helpers;
using AzAcme.Core.Providers.Models;
using Microsoft.Extensions.Logging;

namespace AzAcme.Core.Providers.Route53Dns
{

  public class Route53DnsZone : IDnsZone
  {
    private readonly ILogger logger;
    private readonly AmazonRoute53Client route53Client;
    private readonly string hostedZoneId;

    public Route53DnsZone(ILogger logger, string accessKey, string secretKey, string hostedZoneId, string? awsRegion)
    {
      if (string.IsNullOrWhiteSpace(accessKey))
      {
        throw new ArgumentNullException(nameof(accessKey));
      }

      if (string.IsNullOrWhiteSpace(secretKey))
      {
        throw new ArgumentNullException(nameof(secretKey));
      }

      this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
      this.hostedZoneId = hostedZoneId ?? throw new ArgumentNullException(nameof(hostedZoneId));

      var credentials = new BasicAWSCredentials(accessKey, secretKey);

      route53Client = new AmazonRoute53Client(credentials, RegionEndpoint.GetBySystemName(awsRegion ?? "us-east-1"));
    }

    public async Task<Order> SetTxtRecords(Order order)
    {
      //note this duplicates the same behavior as the azure dns zone provider
      // determine the TXT records needed first, so we validate all before applying.
      foreach (var challenge in order.Challenges)
      {
        var record = DnsHelpers.DetermineTxtRecordName(challenge.Identitifer, this.zoneName);
        challenge.SetRecordName(record);
      }

      foreach (var challenge in order.Challenges)
      {
        await UpdateTxtRecord(challenge);
      }

      return order;
    }



    public Task<Order> RemoveTxtRecords(Order order)
    {
      throw new NotImplementedException();
    }
    private async Task UpdateTxtRecord(DnsChallenge challenge)
    {

      var recordSet = new ResourceRecordSet
      {
        Name = challenge.Identitifer,
        Type = RRType.TXT,
        TTL = 60,
        ResourceRecords = new List<ResourceRecord>
        {
          new ResourceRecord(challenge.TxtValue)
        }
      };

      var request = new ChangeResourceRecordSetsRequest
      {
        HostedZoneId = hostedZoneId,
        ChangeBatch = new ChangeBatch
        {
          Changes = new List<Change>
          {
            new Change
            {
              Action = ChangeAction.UPSERT,
              ResourceRecordSet = recordSet
            }
          }
        }
      };

      var response = await route53Client.ChangeResourceRecordSetsAsync(request);
      logger.LogInformation($"Route53: Updated TXT record for {challenge.TxtRecord}");
    }
  }
}