LinqToServiceNow
================

Linq provider for ServiceNow Soap Web Service

This provider will allow a client to query a reference to a ServiceNow web service using Linq query expressions and methods.

The following methods/expressions are currently supported: 

<ul>
            <li>Select</li>
            <li>Where</li>
            <li>OrderBy</li>
            <li>ThenBy</li>
            <li>OrderByDescending</li>
            <li>ThenByDescending</li>
            <li>GroupBy</li>
            <li>Join</li>
            <li>Take</li>
            <li>TakeWhile</li>
            <li>Skip</li>
            <li>SkipWhile</li>
            <li>ElementAt</li>
            <li>IN</li>
            <li>Like</li>
            <li>Contains</li>
            <li>StartsWith</li>
            <li>EndsWith</li>
            <li>Not</li>
            <li>Or</li>
            <li>And</li>
            <li>ToArray</li>
            <li>ToList</li>
            <li>ToDictionary</li>
</ul>

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
                        select new { Name = c.name, Updated = c.sys_updated_on, OS = c.os }).ToList().Count;
</code>
