using Microsoft.AspNetCore.Mvc;
using OpenTelemetryProductSvc.Models;
using OpenTelemetryProductSvc.Responces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

[ApiController]
[Route("[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Product>>> Get()
    {
        var products = await _productService.GetAllProductsAsync();
        return Ok(products);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProductWithPriceResponse>> Get(Guid id)
    {
        try
        {
            var productWithPrice = await _productService.GetProductByIdAsync(id);
            return Ok(productWithPrice);
        }
        catch (Exception ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost]
    public async Task<ActionResult<Product>> Post([FromBody] Product product)
    {
        await _productService.AddProductAsync(product);
        return CreatedAtAction(nameof(Get), new { id = product.Id }, product);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Put(Guid id, [FromBody] Product updatedProduct)
    {
        try
        {
            await _productService.UpdateProductAsync(id, updatedProduct);
            return NoContent();
        }
        catch (ArgumentException)
        {
            return BadRequest("Product ID mismatch.");
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _productService.DeleteProductAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Product not found.");
        }
    }
}
