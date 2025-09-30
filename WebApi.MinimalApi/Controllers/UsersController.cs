using AutoMapper;
using Microsoft.AspNetCore.Mvc;
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
    [Produces("application/json", "application/xml")]
    public ActionResult<UserDto>? GetUserById([FromRoute] Guid userId)
    {
        var userEntity = _userRepository.FindById(userId);
        if (userEntity is null) return NotFound();
        var userDto = _mapper.Map<UserDto>(userEntity);
        return Ok(userDto);
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
            ModelState.AddModelError("Login", "логин должен состоять только из цифр и букв");
            return UnprocessableEntity(ModelState);
        }

        var userEntity = _mapper.Map<UserEntity>(user);
        _userRepository.Insert(userEntity);
        var guid = userEntity.Id;
        return CreatedAtRoute(
            nameof(GetUserById),
            new { userId = guid }, guid);
    }
}