using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AliGrabApp.Models;

namespace AliGrabApp
{
    class AliContext : DbContext
    {
        public AliContext()
            :base("DbConnection") 
        { }

        public DbSet<AliGroupModel> Groups { get; set; }
        public DbSet<AliItemModel> Items { get; set; }
    }
}
