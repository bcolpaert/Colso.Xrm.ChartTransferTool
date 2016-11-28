using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace Colso.Xrm.ChartTransferTool.AppCode
{
    public class Chart
    {
        private static List<EntityMetadata> sourceEntitiesMetadata;
        private static List<EntityMetadata> targetEntitiesMetadata;
        private static Guid targetCurrentUserId;
        private readonly IOrganizationService sourceService;
        private readonly IOrganizationService targetService;
        private readonly Entity originalRecord;
        private readonly Entity record;

        public Chart(Entity record, IOrganizationService sourceService, IOrganizationService targetService)
        {
            if (sourceEntitiesMetadata == null)
            {
                sourceEntitiesMetadata = new List<EntityMetadata>();
            }

            if (targetEntitiesMetadata == null)
            {
                targetEntitiesMetadata = new List<EntityMetadata>();
            }

            if(targetCurrentUserId == Guid.Empty)
            {
                targetCurrentUserId = ((WhoAmIResponse)targetService.Execute(new WhoAmIRequest())).UserId;
            }

            originalRecord = record;
            this.sourceService = sourceService;
            this.targetService = targetService;

            // Clone view record
            this.record = new Entity(record.LogicalName) { Id = record.Id };
            foreach (var attribute in record.Attributes)
            {
                this.record[attribute.Key] = attribute.Value;
            }
        }

        public void Transfer()
        {
            TransformOwner();
            DoTransfer();
        }

        private void DoTransfer()
        {
            var idfield = string.Concat(record.LogicalName, "id");
            // Check if the view already exists on target organization
            var targetView = targetService.RetrieveMultiple(new QueryExpression(record.LogicalName)
            {
                ColumnSet = new ColumnSet(true),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression(idfield, ConditionOperator.Equal, record.Id)
                    }
                }
            }).Entities.FirstOrDefault();

            if (targetView != null)
            {
                // We need to update the existing view
                CleanEntity(false);
                targetService.Update(record);
            }
            else
            {
                CleanEntity(true);

                var owner = record.GetAttributeValue<EntityReference>("ownerid");
                if (owner != null && owner.LogicalName == "team" || owner.Id != targetCurrentUserId)
                {
                    // Need to create the view for the current user
                    record.Attributes.Remove("ownerid");
                    record.Id = targetService.Create(record);
                    record[idfield] = record.Id;

                    // Share it with ourselves
                    GrantAccessRequest grantRequest = new GrantAccessRequest()
                    {
                        Target = record.ToEntityReference(),
                        PrincipalAccess = new PrincipalAccess()
                        {
                            Principal = new EntityReference("systemuser", targetCurrentUserId),
                            AccessMask = AccessRights.WriteAccess | AccessRights.ReadAccess
                        }
                    };

                    // and then assign it to the team or user
                    record["ownerid"] = owner;
                    targetService.Update(record);
                }
                else
                {
                    targetService.Create(record);
                }
            }
        }

        private void TransformOwner()
        {
            if (record.LogicalName == "userqueryvisualization")
            {
                EntityReference targetReference;
                string name;
                var sourceOwnerRef = record.GetAttributeValue<EntityReference>("ownerid");
                
                if (sourceOwnerRef.LogicalName == "systemuser")
                {
                    var sourceUser = sourceService.Retrieve("systemuser", sourceOwnerRef.Id, new ColumnSet("domainname"));
                    name = sourceUser.GetAttributeValue<string>("domainname");

                    // Let's find the user based on systemuserid or domainname
                    var targetUser = targetService.RetrieveMultiple(new QueryExpression("systemuser")
                    {
                        Criteria = new FilterExpression(LogicalOperator.Or)
                        {
                            Conditions =
                                    {
                                        new ConditionExpression("domainname", ConditionOperator.Equal, name ?? "dummyValueNotExpectedAsDomainNameToAvoidSystemAccount"),
                                        new ConditionExpression("systemuserid", ConditionOperator.Equal, sourceOwnerRef.Id),
                                    }
                        }
                    }).Entities.FirstOrDefault();

                    targetReference = targetUser?.ToEntityReference();
                }
                else
                {
                    var sourceTeam = sourceService.Retrieve("team", sourceOwnerRef.Id, new ColumnSet("name"));
                    name = sourceTeam.GetAttributeValue<string>("name");

                    // Let's find the team based on id or name
                    var targetTeam = targetService.RetrieveMultiple(new QueryExpression("team")
                    {
                        Criteria = new FilterExpression(LogicalOperator.Or)
                        {
                            Conditions =
                                    {
                                        new ConditionExpression("name", ConditionOperator.Equal, name),
                                        new ConditionExpression("teamid", ConditionOperator.Equal, sourceTeam.Id),
                                    }
                        }
                    }).Entities.FirstOrDefault();

                    targetReference = targetTeam?.ToEntityReference();

                }

                if (targetReference != null)
                {
                    record["ownerid"] = targetReference;
                }
                else
                {
                    throw new Exception(string.Format(
                        "Unable to find a user or team in the target organization with name '{0}' or id '{1}'",
                        name,
                        record.GetAttributeValue<EntityReference>("ownerid").Id));
                }
            }
        }

        private void CleanEntity(bool create)
        {
            var targetEntityMetadata = targetEntitiesMetadata.FirstOrDefault(emd => emd.LogicalName == record.LogicalName);
            if (targetEntityMetadata == null || targetEntityMetadata.Attributes == null)
            {
                targetEntityMetadata = MetadataHelper.GetEntityMetadata(record.LogicalName, EntityFilters.Entity | EntityFilters.Attributes, targetService);

                var existingEmd = targetEntitiesMetadata.FirstOrDefault(emd => emd.LogicalName == record.LogicalName);
                if(existingEmd != null)
                {
                    targetEntitiesMetadata.Remove(existingEmd);
                }
                
                targetEntitiesMetadata.Add(targetEntityMetadata);
            }

            foreach (var attribute in targetEntityMetadata.Attributes)
            {
                if (create && !attribute.IsValidForCreate.Value && record.Contains(attribute.LogicalName))
                {
                    record.Attributes.Remove(attribute.LogicalName);
                }

                if (!create && !attribute.IsValidForUpdate.Value && record.Contains(attribute.LogicalName))
                {
                    record.Attributes.Remove(attribute.LogicalName);
                }
            }
        }
    }
}