using System;

namespace Inedo.Extensions.Shared.Mercurial.Clients
{
    [Serializable]
    public sealed class MercurialUpdateOptions
    {
        public MercurialUpdateOptions()
        {
        }

        public string Tag { get; set; }
        public string Branch { get; set; }
    }
}
