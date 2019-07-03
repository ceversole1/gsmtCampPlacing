using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace gsmtCampPlacing.Models
{
    public class EmailAttachment
    {
        public List<EmailAttachmentMeta> ListOfEmailAttachments { get; set; }
    }

    public class EmailAttachmentMeta
    {
        public byte[] bytes { get; set; }
    }
}
