using ECommerce.Application.Repositories;
using ECommerce.Application.Services;
using ECommerce.Domain.DTOs;
using ECommerce.Domain.Entities.Concretes;
using ECommerce.Domain.Helpers;
using ECommerce.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace ECommerce.WebApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IReadAppUserRepository _readAppUserRepository;
    private readonly IWriteAppUserRepository _writeAppUserRepository;
    private readonly ITokenService _tokenService;

    public AuthController(IReadAppUserRepository readAppUserRepository, ITokenService tokenService, IWriteAppUserRepository writeAppUserRepository)
    {
        _readAppUserRepository = readAppUserRepository;
        _tokenService = tokenService;
        _writeAppUserRepository = writeAppUserRepository;
    }

    [HttpPost("[action]")]
    public async Task<IActionResult> Login([FromBody] LoginDTO loginDTO)
    {
        var user = await _readAppUserRepository.GetUserByUserName(loginDTO.UserName);
        if (user is null)
            return BadRequest("Invalid username");

        if (user.ConfirmEmail is false)
            return BadRequest("Email not confirmed");


        using var hmac = new HMACSHA256(user.PasswordSalt);
        var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(loginDTO.Password));

        var isPasswordMatch = computedHash.SequenceEqual(user.PasswordHash);
        if (!isPasswordMatch)
            return BadRequest("Invalid password");

        var accessToken = _tokenService.CreateAccessToken(user);

        var refreshToken = _tokenService.CreateRefreshToken();
        SetRefreshToken(user, refreshToken);

        return Ok(new { accessToken = accessToken });
    }


    [HttpPost("RefreshToken")]
    public async Task<IActionResult> RefreshToken()
    {
        var refreshToken = Request.Cookies["refreshToken"];
        if (string.IsNullOrEmpty(refreshToken))
            return BadRequest("Invalid refresh token");

        var user = await _readAppUserRepository.GetUserByRefreshToken(refreshToken);
        if (user is null)
            return BadRequest("Invalid refresh token");

        var accessToken = _tokenService.CreateAccessToken(user);

        var refreshTokenObj = _tokenService.CreateRefreshToken();
        SetRefreshToken(user, refreshTokenObj);

        return Ok(new { accessToken = accessToken });
    }



    // Helper Method. SetRefreshToken
    private void SetRefreshToken(AppUser user, RefreshToken refreshToken)
    {
        var cookieOptions = new CookieOptions()
        {
            HttpOnly = true,
            Expires = refreshToken.ExpireTime
        };

        Response.Cookies.Append("refreshToken", refreshToken.Token, cookieOptions);

        user.RefreshToken = refreshToken.Token;
        user.RefreshTokenCreateTime = refreshToken.CreateTime;
        user.RefreshTokenExpireTime = refreshToken.ExpireTime;

        _writeAppUserRepository.UpdateAsync(user);
        _writeAppUserRepository.SaveChangeAsync();
    }


    // Add User Method
    [HttpPost("[action]")]
    public async Task<IActionResult> Register([FromBody] AppUserDTO appUserDTO)
    {
        var user = await _readAppUserRepository.GetUserByUserName(appUserDTO.UserName);
        if (user is not null)
            return BadRequest("User already exists");

        using var hmac = new HMACSHA256();

        var newUser = new AppUser()
        {
            UserName = appUserDTO.UserName,
            Email = appUserDTO.Email,
            PasswordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(appUserDTO.Password)),
            PasswordSalt = hmac.Key,
            Role = appUserDTO.Role,
            ConfirmEmail = false
        };


        var confirmEmailToken = _tokenService.CreateConfirmEmailToken();
        var actionUrl = $@"https://localhost:5046/api/Auth/ConfirmEmail?token={confirmEmailToken.Token}";
        var result = await SmtpService.SendMail(appUserDTO.Email, "Confirm Your Email", $"<a href='{actionUrl}'>Confirm Your Email</a>");

        newUser.ConfirmEmailToken = confirmEmailToken.Token;
        newUser.ConfirmEmailTokenCreateTime = confirmEmailToken.CreateTime;
        newUser.ConfirmEmailTokenExpireTime = confirmEmailToken.ExpireTime;

        await _writeAppUserRepository.AddAsync(newUser);
        await _writeAppUserRepository.SaveChangeAsync();
        return Ok();
    }





    [HttpPost("[action]")]
    public async Task<IActionResult> ConfirmEmail([FromQuery] string token)
    {

        var user = await _readAppUserRepository.GetUserByConfirmEmailToken(token);
        if (user is null)
            return BadRequest("Invalid User");

        if (user.ConfirmEmailTokenExpireTime < DateTime.UtcNow)
            return BadRequest("Expired Token");

        user.ConfirmEmail = true;

        await _writeAppUserRepository.UpdateAsync(user);
        await _writeAppUserRepository.SaveChangeAsync();
        return Ok();
    }








    [Authorize(Roles = "Admin")]
    [HttpGet("[action]")]
    public IActionResult SomeMethod()
    {
        var identity = HttpContext.User.Identity as ClaimsIdentity;
        var claims = identity.Claims;

        var user = new AppUser()
        {
            UserName = claims.FirstOrDefault(p => p.Type == ClaimTypes.Name)?.Value,
            Email = claims.FirstOrDefault(p => p.Type == ClaimTypes.Email)?.Value,
            Role = claims.FirstOrDefault(p => p.Type == ClaimTypes.Role)?.Value
        };

        return Ok(user);
    }




    // Forgote Passowrd
    [HttpPost("[action]")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDTO forgotPasswordDTO)
    {
        var user = await _readAppUserRepository.GetUserByEmail(forgotPasswordDTO.Email);
        if (user is null)
            return BadRequest("User not found");

        var repasswordToken = _tokenService.CreateRepasswordToken();
        var actionUrl = $@"https://localhost:5001/api/Auth/ResetPassword?token={repasswordToken.Token}";

        var result = await SmtpService.SendMail(forgotPasswordDTO.Email, "Reset Password", $"Reset your password <a href='{actionUrl}'></a>");

        user.RePasswordToken = repasswordToken.Token;
        user.RePasswordTokenCreateTime = repasswordToken.CreateTime;
        user.RePasswordTokenExpireTime = repasswordToken.ExpireTime;

        await _writeAppUserRepository.UpdateAsync(user);
       await _writeAppUserRepository.SaveChangeAsync();

        return Ok(new { actionUrl = actionUrl });
    }

    [HttpPost("[action]")]
    public async Task<IActionResult> ResetPassword([FromQuery] string token, [FromBody] ResetPasswordDTO resetPasswordDTO)
    {
     
        var user = await _readAppUserRepository.GetUserByRePasswordToken(token);
        if (user is null)
            return BadRequest("Invalid RePasswordToken");

        if(user.RePasswordTokenExpireTime < DateTime.UtcNow)
            return BadRequest("RePasswordToken expired");

        using var hmac = new HMACSHA256();

        user.PasswordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(resetPasswordDTO.Password));
        user.PasswordSalt = hmac.Key;

        await _writeAppUserRepository.UpdateAsync(user);
        await _writeAppUserRepository.SaveChangeAsync();
        return Ok();
    }
    


}
