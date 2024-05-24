using AutoMapper;
using Mango.Services.ProductAPI.Data;
using Mango.Services.ProductAPI.Models;
using Mango.Services.ProductAPI.Models.Dto;
using Mango.Services.ProductAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mango.Services.ProductAPI.Controllers
{
    [Route("api/product")]
    [ApiController]
    public class ProductAPIController : ControllerBase
    {
        private readonly AppDbContext _db;
        private ResponseDto _response;
        private IMapper _mapper;
        private readonly IAzureStorage _azureStorage;

        public ProductAPIController(AppDbContext db, IMapper mapper, IAzureStorage azureStorage)
        {
            _db = db;
            _mapper = mapper;
            _response = new ResponseDto();
            _azureStorage = azureStorage;
        }

        [HttpGet]
        public ResponseDto Get()
        {
            try
            {
                IEnumerable<Product> objList = _db.Products.ToList();
                _response.Result = _mapper.Map<IEnumerable<ProductDto>>(objList);
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.Message = ex.Message;
            }
            return _response;
        }

        [HttpGet]
        [Route("{id:int}")]
        public ResponseDto Get(int id)
        {
            try
            {
                Product obj = _db.Products.First(u=>u.ProductId==id);
                _response.Result = _mapper.Map<ProductDto>(obj);
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.Message = ex.Message;
            }
            return _response;
        }

        [HttpPost]
        [Authorize(Roles = "ADMIN")]
        public async Task<ResponseDto> Post([FromForm] ProductDto ProductDto)
        {
            try
            {
                Product product = _mapper.Map<Product>(ProductDto);
                _db.Products.Add(product);
                _db.SaveChanges();

                if (ProductDto.Image != null)
                {
                    BlobResponseDto blobResponseDto = await _azureStorage.UploadAsync(ProductDto.Image);

                    // Check if we got an error
                    if (blobResponseDto.Error == true)
                    {
                        _response.IsSuccess = false;
                        _response.Message = "Unable to upload image";
                        return _response;
                    }
                    
                    product.ImageUrl = blobResponseDto.Blob.Uri;
                    product.ImageLocalPath = blobResponseDto.Blob.Name; // Fake column :>
                   
                    // string fileName = product.ProductId + Path.GetExtension(ProductDto.Image.FileName);
                    // string filePath = @"wwwroot\ProductImages\" + fileName;

                    // //I have added the if condition to remove the any image with same name if that exist in the folder by any change
                    //     var directoryLocation = Path.Combine(Directory.GetCurrentDirectory(), filePath);
                    //     FileInfo file = new FileInfo(directoryLocation);
                    //     if (file.Exists)
                    //     {
                    //         file.Delete();
                    //     }

                    // var filePathDirectory = Path.Combine(Directory.GetCurrentDirectory(), filePath);
                    // using (var fileStream = new FileStream(filePathDirectory, FileMode.Create))
                    // {
                    //     ProductDto.Image.CopyTo(fileStream);
                    // }
                    // var baseUrl = $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host.Value}{HttpContext.Request.PathBase.Value}";
                    // product.ImageUrl = baseUrl+ "/ProductImages/"+ fileName;
                    // product.ImageLocalPath = filePath;
                }
                else
                {
                    product.ImageUrl = "https://placehold.co/600x400";
                }

                _db.Products.Update(product);
                _db.SaveChanges();
                _response.Result = _mapper.Map<ProductDto>(product);
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.Message = ex.Message;
            }
            return _response;
        }


        [HttpPut]
        [Authorize(Roles = "ADMIN")]
        public async Task<ResponseDto> Put([FromForm] ProductDto ProductDto)
        {
            try
            {
                Product product = _mapper.Map<Product>(ProductDto);

                if (ProductDto.Image != null)
                {
                    if (!string.IsNullOrEmpty(product.ImageLocalPath))
                    {
                        BlobResponseDto deleteResponse = await _azureStorage.DeleteAsync(product.ImageLocalPath);
                    }

                    BlobResponseDto uploadResponse = await _azureStorage.UploadAsync(ProductDto.Image);

                    // Check if we got an error
                    if (uploadResponse.Error == true)
                    {
                        _response.IsSuccess = false;
                        _response.Message = "Unable to upload image";
                        return _response;
                    }
                    
                    product.ImageUrl = uploadResponse.Blob.Uri;
                    product.ImageLocalPath = uploadResponse.Blob.Name; // Fake column :>

                    // if (!string.IsNullOrEmpty(product.ImageLocalPath))
                    // {
                    //     var oldFilePathDirectory = Path.Combine(Directory.GetCurrentDirectory(), product.ImageLocalPath);
                    //     FileInfo file = new FileInfo(oldFilePathDirectory);
                    //     if (file.Exists)
                    //     {
                    //         file.Delete();
                    //     }
                    // }

                    // string fileName = product.ProductId + Path.GetExtension(ProductDto.Image.FileName);
                    // string filePath = @"wwwroot\ProductImages\" + fileName;
                    // var filePathDirectory = Path.Combine(Directory.GetCurrentDirectory(), filePath);
                    // using (var fileStream = new FileStream(filePathDirectory, FileMode.Create))
                    // {
                    //     ProductDto.Image.CopyTo(fileStream);
                    // }
                    // var baseUrl = $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host.Value}{HttpContext.Request.PathBase.Value}";
                    // product.ImageUrl = baseUrl + "/ProductImages/" + fileName;
                    // product.ImageLocalPath = filePath;
                }


                _db.Products.Update(product);
                _db.SaveChanges();

                _response.Result = _mapper.Map<ProductDto>(product);
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.Message = ex.Message;
            }
            return _response;
        }

        [HttpDelete]
        [Route("{id:int}")]
        [Authorize(Roles = "ADMIN")]
        public async Task<ResponseDto> Delete(int id)
        {
            try
            {
                Product obj = _db.Products.First(u=>u.ProductId==id);
                if (!string.IsNullOrEmpty(obj.ImageLocalPath))
                {
                    BlobResponseDto blobResponseDto = await _azureStorage.DeleteAsync(obj.ImageLocalPath);

                    // var oldFilePathDirectory = Path.Combine(Directory.GetCurrentDirectory(), obj.ImageLocalPath);
                    // FileInfo file = new FileInfo(oldFilePathDirectory);
                    // if (file.Exists)
                    // {
                    //     file.Delete();
                    // }
                }

                _db.Products.Remove(obj);
                _db.SaveChanges();
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.Message = ex.Message;
            }
            return _response;
        }
    }
}
