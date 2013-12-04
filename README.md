LinqToServiceNow
================

Linq provider for ServiceNow Soap Web Service

This provider will allow a client to query a reference to a ServiceNow web service using Linq query expressions and methods.

The following methods/expressions are currently supported: select where, orderby, Take, Skip, Range, IN, Like, Contains, Not, Or, And.

The current limitations are due to what can be done through the underlying ServiceNow web service interface.

Example:
<code>

            var ComputerRepository =
                        new ServiceNowRepository<[MyServiceNowReference].ServiceNowSoapClient, 
                                                [MyServiceNowReference].getRecords, 
                                                [MyServiceNowReference].getRecordsResponseGetRecordsResult>();

            int myCount = (from c in ComputerRepository
                           where ((DateTime.Parse("2006-01-01") < DateTime.Parse(c.sys_updated_on)
                           & new string[] { "1", "7" }.Contains(c.install_status))
                           & ("D9CMSD41" == c.name | c.name.Contains("R")))
                           orderby c.name
                           select new { Name = c.name, Updated = c.sys_updated_on, OS = c.os })
                           .Range(5, 5).ToList().Count;
</code>
