using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AliGrabApp.Models
{
    public class AliItemModel
    {
        [Key]
        //[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public string Title { get; set; }        
        public string Price { get; set; }
        public string PriceCurrency { get; set; }
        public string Unit { get; set; }
        public string Seller { get; set; }
        public string Link { get; set; }
        public string Description { get; set; }
        [MaxLength]
        public byte[] Image { get; set; }

        public AliItemModel()
        {
            Id = Guid.NewGuid();
        }
    }

    public class AliItem : INotifyPropertyChanged
    {
        private Guid _id;
        private long _no;
        private string _title;
        private string _price;
        private string _priceCurrency;
        private string _unit;
        private string _seller;
        private string _link;
        private string _description;
        private byte[] _image;

        public AliItem()
        {
            Id = Guid.NewGuid();
        }

        public Guid Id
        {
            get { return _id; }
            set
            {
                if (_id != value)
                {
                    _id = value;
                    RaisePropertyChanged(nameof(Id));
                }
            }
        }

        public long No
        {
            get { return _no; }
            set
            {
                if (_no != value)
                {
                    _no = value;
                    RaisePropertyChanged(nameof(No));
                }
            }
        }

        public byte[] Image
        {
            get { return _image; }
            set
            {
                if (_image != value)
                {
                    _image = value;
                    RaisePropertyChanged(nameof(Image));
                }
            }
        }

        public string Title
        {
            get { return _title; }
            set
            {
                if (_title != value)
                {
                    _title = value;
                    RaisePropertyChanged(nameof(Title));
                }
            }
        }

        public string Price
        {
            get { return _price; }
            set
            {
                if (_price != value)
                {
                    _price = value;
                    RaisePropertyChanged(nameof(Price));
                }
            }
        }

        public string PriceCurrency
        {
            get { return _priceCurrency; }
            set
            {
                if (_priceCurrency != value)
                {
                    _priceCurrency = value;
                    RaisePropertyChanged(nameof(PriceCurrency));
                }
            }
        }

        public string Unit
        {
            get { return _unit; }
            set
            {
                if (_unit != value)
                {
                    _unit = value;
                    RaisePropertyChanged(nameof(Unit));
                }
            }
        }

        public string Seller
        {
            get { return _seller; }
            set
            {
                if (_seller != value)
                {
                    _seller = value;
                    RaisePropertyChanged(nameof(Seller));
                }
            }
        }

        public string Description
        {
            get { return _description; }
            set
            {
                if (_description != value)
                {
                    _description = value;
                    RaisePropertyChanged(nameof(Description));
                }
            }
        }

        public string Link
        {
            get { return _link; }
            set
            {
                if (_link != value)
                {
                    _link = value;
                    RaisePropertyChanged(nameof(Link));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void RaisePropertyChanged(string property)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(property));
            }
        }
    }
}
