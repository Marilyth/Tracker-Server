/*using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using Newtonsoft.Json;
using MopsBot.Data.Entities;
using System.Threading.Tasks;

namespace MopsBot.Api.Controllers
{
    [Route("api/[controller]")]
    public class UserController : Controller
    {
        public UserController(){

        }
        /*
        [HttpGet]
        public IEnumerable<KeyValuePair<ulong,User>> GetAll()
        {
            Response.Headers.Add("Access-Control-Allow-Origin", "*");
            return StaticBase.people.Users.ToList();
        }

        [HttpGet("{id}", Name = "GetUser")]
        public IActionResult GetById(ulong id)
        {   
            Response.Headers.Add("Access-Control-Allow-Origin", "*");
            User item = null;
            try{
                 item = StaticBase.people.Users.First(x=> x.Key==id).Value;
            }catch{}
            if (item == null)
            {
                return NotFound();
            }
            return new ObjectResult(item);
        }

        [HttpGet("guilds/{id}", Name = "GetUserGuilds")]
        public async Task<IActionResult> GetGuilds(ulong id)
        {
            var infoDict = new Dictionary<ulong, Discord.GuildPermissions>();
            var user = Program.Client.GetUser(id);
            foreach(var guild in user.MutualGuilds){
                infoDict.Add(guild.Id, guild.GetUser(id).GuildPermissions);
            }
            
            return new ObjectResult(JsonConvert.SerializeObject(infoDict, Formatting.Indented));
        }
    }
}*/