using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HotelCatalog.Bll.DTOs
{
    public class QueryParameters
    {
        private const int MaxPageSize = 50;
        private int _pageSize = 10;

        public int Page { get; set; } = 1;

        public int PageSize
        {
            get => _pageSize;
            set => _pageSize = value > MaxPageSize ? MaxPageSize : value;
        }

        public string? SortBy { get; set; }
        public string? SortOrder { get; set; } = "asc";
        public string? SearchTerm { get; set; }
    }

    public class ServiceQueryParameters : QueryParameters
    {
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
    }

    public class DiscountQueryParameters : QueryParameters
    {
        public decimal? MinPercent { get; set; }
        public decimal? MaxPercent { get; set; }
    }

    public class RoomCategoryQueryParameters : QueryParameters
    {
        // Can add specific filters for room categories if needed in the future
        // For now, inherits SearchTerm, SortBy, SortOrder, Page, PageSize
    }
}