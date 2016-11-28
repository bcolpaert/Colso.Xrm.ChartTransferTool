using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Colso.Xrm.ChartTransferTool.AppCode
{
    /// <summary>
    /// Helps to interact with Crm Charts
    /// </summary>
    internal class ChartHelper
    {
        /// <summary>
        /// Retrieve the list of Charts 
        /// </summary>
        /// <param name="service">Organization Service</param>
        /// <returns>List of Charts</returns>
        public static List<Entity> RetrieveCharts(string entity, IOrganizationService service)
        {
            try
            {
                var results = service.RetrieveMultiple(new QueryExpression("savedqueryvisualization")
                {
                    ColumnSet = new ColumnSet(true),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("primaryentitytypecode", ConditionOperator.Equal, entity)
                        }
                    }
                });

               return results.Entities.ToList();
            }
            catch (Exception error)
            {
                string errorMessage = CrmExceptionHelper.GetErrorMessage(error, false);
                throw new Exception("Error while retrieving Charts: " + errorMessage);
            }
        }

        public static List<Entity> RetrieveUserCharts(string entity, IOrganizationService service)
        {
            try
            {
                var results = service.RetrieveMultiple(new QueryExpression("userqueryvisualization")
                {
                    ColumnSet = new ColumnSet(true),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("primaryentitytypecode", ConditionOperator.Equal, entity)
                        }
                    }
                });

                return results.Entities.ToList();
            }
            catch (Exception error)
            {
                string errorMessage = CrmExceptionHelper.GetErrorMessage(error, false);
                throw new Exception("Error while retrieving user Charts: " + errorMessage);
            }
        }
    }
}