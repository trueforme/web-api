using AutoMapper;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using WebApi.MinimalApi.Domain;
using WebApi.MinimalApi.Models;

namespace WebApi.MinimalApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UsersController : Controller
{
    private readonly IUserRepository _userRepository;
    private readonly IMapper _mapper;

    // Чтобы ASP.NET положил что-то в userRepository требуется конфигурация
    public UsersController(IUserRepository userRepository, IMapper mapper)
    {
        _userRepository = userRepository;
        _mapper = mapper;
    }

    [HttpGet("{userId}", Name = nameof(GetUserById))]
    [HttpHead("{userId}")]
    [Produces("application/json", "application/xml")]
    public ActionResult<UserDto>? GetUserById([FromRoute] Guid userId)
    {
        var userEntity = _userRepository.FindById(userId);
        if (userEntity is null) return NotFound();
        var userDto = _mapper.Map<UserDto>(userEntity);
        if (!HttpMethods.IsHead(Request.Method)) return Ok(userDto);
        Response.Headers.ContentType = "application/json; charset=utf-8";
        return Ok();
    }

    [HttpGet("", Name = nameof(GetUsers))]
    [Produces("application/json", "application/xml")]
    public IActionResult GetUsers([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Max(1, pageSize);
        pageSize = Math.Min(20, pageSize);
        var users = _userRepository.GetPage(pageNumber, pageSize);
        var paginationHeader = new
        {
            previousPageLink = users.HasPrevious
                ? Url.Link(nameof(GetUsers), new
                {
                    pageNumber = pageNumber - 1,
                    pageSize = pageSize
                })
                : null,
            nextPageLink = users.HasNext
                ? Url.Link(nameof(GetUsers), new
                {
                    pageNumber = pageNumber + 1,
                    pageSize = pageSize
                })
                : null,
            totalCount = users.TotalCount,
            pageSize = pageSize,
            currentPage = users.CurrentPage,
            totalPages = users.TotalPages,
        };
        Response.Headers.Add("X-Pagination", JsonConvert.SerializeObject(paginationHeader));
        return Ok(users);
    }

    [HttpPost]
    [Produces("application/json", "application/xml")]
    public IActionResult CreateUser([FromBody] UserCreateDto user)
    {
        if (user is null)
            return BadRequest();
        if (!ModelState.IsValid)
            return UnprocessableEntity(ModelState);
        if (!user.Login.All(char.IsLetterOrDigit))
        {
            ModelState.AddModelError("Login", "Login should contain only letters or digits");
            return UnprocessableEntity(ModelState);
        }

        var userEntity = _mapper.Map<UserEntity>(user);
        userEntity = _userRepository.Insert(userEntity);
        var guid = userEntity.Id;
        return CreatedAtRoute(
            nameof(GetUserById),
            new { userId = guid }, guid);
    }

    [HttpPut("{userId}")]
    [Produces("application/json", "application/xml")]
    public IActionResult UpsertUser(Guid userId, [FromBody] UserUpdateDto user)
    {
        if (userId == Guid.Empty || user is null)
            return BadRequest();
        if (!ModelState.IsValid)
            return UnprocessableEntity(ModelState);
        var userEntity = _userRepository.FindById(userId);
        userEntity ??= new UserEntity(userId);
        userEntity = _mapper.Map(user, userEntity);
        _userRepository.UpdateOrInsert(userEntity, out var isInserted);
        if (isInserted)
            return CreatedAtRoute(
                nameof(GetUserById),
                new { userId }, new { guid = userId });

        return NoContent();
    }

    [HttpDelete("{userId}")]
    public IActionResult DeleteUSer(Guid userId)
    {
        if (userId == Guid.Empty || _userRepository.FindById(userId) is null)
            return NotFound();
        _userRepository.Delete(userId);
        return NoContent();
    }


    [HttpOptions("")]
    public IActionResult Options()
    {
        Response.Headers.Add("Allow", "POST, GET, OPTIONS");
        return Ok();
    }

    [HttpPatch("{userId}")]
    [Produces("application/json", "application/xml")]
    public IActionResult PartiallyUpdateUser(Guid userId, [FromBody] JsonPatchDocument<UserUpdateDto> patchDoc)
    {
        if (patchDoc is null) return BadRequest();
        var user = _userRepository.FindById(userId);
        if (user is null) return NotFound();
        var updateDto = _mapper.Map<UserUpdateDto>(user);
        patchDoc.ApplyTo(updateDto, ModelState);
        if (!TryValidateModel(updateDto))
            return UnprocessableEntity(ModelState);
        user = _mapper.Map(updateDto, user);
        _userRepository.UpdateOrInsert(user, out var inserted);
        return NoContent();
    }
}