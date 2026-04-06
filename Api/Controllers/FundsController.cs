using Api.Configuration;
using Api.DataFiles;
using Api.Queries;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Api.Controllers
{
    // any methods defined in this controller will be mapped to version 1
    // also defined a more RESTful route, where the base resource is accessed via /api/v(x)/Funds   
    [ApiVersion(1.0)]   
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]   
    public class FundsController : ControllerBase
    {
        // The defined route didn't follow REST best practices
        // In order to request the resource, I needed to type "https://localhost:5001/get-funds"
        // I have now routed(?) it as follows "https://localhost:5001/api/{version}/Funds/{id} (see controller comments)
        // I have also changed the type of "id" from a string to Guid (see comment in method)
        // Lastly I have made this method asynchronous so it doesn't block while awaiting the API call (this makes the app more scalable as we can handle more user requests efficiently)
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(
            Guid id,
            [FromServices] IMemoryCache cache,
            [FromServices] IOptionsSnapshot<CachingOptions> options)
        {
            // guarded the search to throw a 404 code on search failure (see further comments below)
            try
            {
                // configure caching to reduce number of API/database calls on repeat requests
                // you can use IDistributed cache with a database and I would recommend that for a scalable production service (this is just for demo purposes)
                if (!cache.TryGetValue(id, out FundDetails fund))
                {
                    // configurable expiration times that will update per request, simply update the JSON file (no downtime)
                    MemoryCacheEntryOptions cacheEntryOptions = new MemoryCacheEntryOptions()
                        .SetSlidingExpiration(offset: TimeSpan.FromMinutes(options.Value.SlidingExpirationMinutes))
                        .SetAbsoluteExpiration(relative: TimeSpan.FromMinutes(options.Value.AbsoluteExpirationMinutes));

                    // this is not the final API implementation so it's hard to know, BUT
                    // this is currently fetching all available funds, which is inefficient as we have to load all results into memory
                    // if the final API has a searchable or paging based resource to fetch, use that. OTHERWISE 
                    // the next best option is to keep our own records of this data in a database, synced up with a job, and then page our database
                    string file = await System.IO.File.ReadAllTextAsync("./DataFiles/funds.json");
                    List<FundDetails> funds = JsonConvert.DeserializeObject<List<FundDetails>>(file);

                    // remove null check as accessing via URI rather than a query
                    // implemented the query in the method below instead
                    // if (id != null)
                    // {
                    //     // this is searching against the wrong property, we should be searching against "id"
                    //     // there is no appropriate error handling if there is no matching resource
                    //     //      it will produce an unhandled exception which will surface a HTTP 500 to the client
                    //     //      that kind of error is a bit generic and indicates something has gone wrong rather than the search returning no results
                    //      return Ok(funds.Single(x => x.MarketCode == id));                        
                    // }

                    // corrected the search criteria (and modified search type as the destination type is a Guid)
                    fund = funds.Single(x => x.Id == id); 

                    // set into the cache for re-use if needed
                    cache.Set(id, fund, cacheEntryOptions);
                }    

                return Ok(fund);       
            }
            catch (Exception)
            {
                return NotFound();
            }    
        }

        // Similar to the above endpoint, this route didn't follow REST best practices
        // Updated from "https://localhost:5001/get-managerfunds" -> "https://localhost:5001/api/{version}/Funds?manager=x
        // This now functions as a "get all" method that can be filtered down by queries. The existing functionality is retained as part of this.
        // Also made the method async for the same reasons as above
        [HttpGet]
        public async Task<IActionResult> GetFunds(
            [FromQuery] FundDetailsQuery query,
            [FromServices] IMemoryCache cache,
            [FromServices] IOptionsSnapshot<CachingOptions> options)
        {
            Func<Task<IEnumerable<FundDetails>>> request = new(async () =>
            {
                string file = await System.IO.File.ReadAllTextAsync("./DataFiles/funds.json");
                IEnumerable<FundDetails> funds = JsonConvert.DeserializeObject<IEnumerable<FundDetails>>(file); 

                return funds;
            });

            // no benefit to caching an unfiltered result
            // (this nullability check is currently weak to regression - if a new property were added
            // to the query and wasn't checked here, it wouldn't get searched)
            // I simply ran out of time to implement a better solution
            if (string.IsNullOrEmpty(query.Manager))
            {
                return Ok(await request.Invoke());
            }

            string cacheKey = JsonConvert.SerializeObject(query);

            // and the new query fields will already work with the caching
            if (!cache.TryGetValue(cacheKey, out IEnumerable<FundDetails> funds))
            {
                MemoryCacheEntryOptions cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(offset: TimeSpan.FromMinutes(options.Value.SlidingExpirationMinutes))
                    .SetAbsoluteExpiration(relative: TimeSpan.FromMinutes(options.Value.AbsoluteExpirationMinutes));

                funds = await request.Invoke();

                if (!string.IsNullOrEmpty(query.Manager))
                {
                    // this was previously searching against the wrong property (previous search was against name)
                    funds = funds.Where(x => x.FundManager == query.Manager);
                }

                cache.Set(cacheKey, funds, cacheEntryOptions);
            }    

            return Ok(funds);
        }
    }
}