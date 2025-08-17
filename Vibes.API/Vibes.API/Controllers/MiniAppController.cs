using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Vibes.API.Configuration;

namespace Vibes.API.Controllers;

[ApiController]
[Route("ui/[controller]")]
public class MiniAppController(IOptions<BotConfiguration> config, ILogger<BotController> logger) : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        var html = """
                   <!DOCTYPE html>
                   <html lang="ru" class="scroll-smooth">
                   <head>
                       <meta charset="UTF-8">
                       <meta name="viewport" content="width=device-width, initial-scale=1.0">
                       <title>Vibes - Трекер ментального здоровья и продуктивности</title>
                       <meta name="description" content="Vibes - это умный трекер для отслеживания вашего ментального состояния, формирования полезных привычек и достижения гармонии.">
                   
                       <!-- TailwindCSS CDN -->
                       <script src="https://cdn.tailwindcss.com"></script>
                   
                       <!-- Google Fonts: Manrope -->
                       <link rel="preconnect" href="https://fonts.googleapis.com">
                       <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
                       <link href="https://fonts.googleapis.com/css2?family=Manrope:wght@300;400;500;600;700;800&display=swap" rel="stylesheet">
                   
                       <script>
                           // Custom TailwindCSS Configuration
                           tailwind.config = {
                               theme: {
                                   extend: {
                                       fontFamily: {
                                           sans: ['Manrope', 'sans-serif'],
                                       },
                                       colors: {
                                           'brand-bg': '#0D1117',
                                           'brand-surface': '#161B22',
                                           'brand-primary': '#58A6FF',
                                           'brand-secondary': '#8B949E',
                                           'brand-accent': '#3FB950',
                                       }
                                   }
                               }
                           }
                       </script>
                   
                       <style>
                           /* Custom styles for scroll animations */
                           .reveal {
                               opacity: 0;
                               transform: translateY(30px);
                               transition: opacity 0.8s ease-out, transform 0.6s ease-out;
                               will-change: opacity, transform;
                           }
                           .reveal.visible {
                               opacity: 1;
                               transform: translateY(0);
                           }
                       </style>
                   </head>
                   
                   <body class="bg-brand-bg text-brand-secondary antialiased">
                   
                   <!-- Header -->
                   <header class="py-6 px-4 md:px-8 absolute top-0 left-0 w-full z-10">
                       <div class="container mx-auto flex justify-between items-center">
                           <h1 class="text-2xl font-bold text-white">Vibes</h1>
                           <a href="https://t.me/myvibeappbot" target="_blank" class="bg-brand-primary text-white font-semibold py-2 px-5 rounded-lg hover:bg-opacity-90 transition-colors">
                               Запустить бота
                           </a>
                       </div>
                   </header>
                   
                   <main>
                   
                       <!-- 1. Hero Section -->
                       <section class="relative min-h-screen flex items-center justify-center text-center overflow-hidden pt-24 pb-12 px-4">
                           <div class="absolute inset-0 bg-gradient-to-b from-brand-bg via-brand-bg to-transparent opacity-50 z-0"></div>
                           <div class="absolute -top-1/4 -right-1/4 w-1/2 h-1/2 bg-brand-primary/10 rounded-full blur-3xl animate-pulse"></div>
                           <div class="absolute -bottom-1/4 -left-1/4 w-1/2 h-1/2 bg-brand-accent/10 rounded-full blur-3xl animate-pulse delay-1000"></div>
                   
                           <div class="container mx-auto max-w-3xl relative z-10 reveal">
                               <h1 class="text-4xl md:text-6xl font-extrabold text-white mb-4 leading-tight">
                                   Ваш AI-ключ к ментальному балансу
                               </h1>
                               <p class="text-lg md:text-xl max-w-2xl mx-auto mb-8 text-brand-secondary">
                                   Vibes анализирует ваш сон, календарь и привычки, чтобы помочь вам управлять энергией и избегать выгорания.
                               </p>
                               <a href="https://t.me/myvibeappbot" target="_blank" class="bg-brand-primary text-white font-bold py-4 px-10 rounded-lg text-xl hover:scale-105 transition-transform transform inline-flex items-center">
                                   <svg class="w-6 h-6 mr-3" fill="currentColor" viewBox="0 0 24 24" aria-hidden="true"><path d="M9.78 18.65l.28-4.23 7.68-6.92c.34-.31-.07-.46-.52-.19L7.74 13.3 3.64 12c-.88-.25-.89-.86.2-1.3l15.97-6.16c.73-.33 1.43.18 1.15 1.3l-2.72 12.81c-.19.91-.74 1.13-1.51.71l-4.84-3.56-2.31 2.24c-.25.24-.45.44-.88.44l.21-4.58z"></path></svg>
                                   Запустить в Telegram
                               </a>
                           </div>
                       </section>
                   
                       <!-- Остальные секции (Pains, About, Features) остаются без изменений -->
                       <!-- ... -->
                       <section id="pains" class="py-16 md:py-24 px-4">
                           <!-- ... -->
                       </section>
                       <section id="about" class="py-16 md:py-24 px-4 overflow-hidden">
                           <!-- ... -->
                       </section>
                       <section id="features" class="py-16 md:py-24 px-4 bg-brand-surface border-y border-gray-800">
                           <!-- ... -->
                       </section>
                   
                       <!-- 5. Call To Action Section (ИЗМЕНЕННЫЙ) -->
                       <section id="cta" class="py-16 md:py-32 px-4">
                           <div class="container mx-auto max-w-2xl text-center reveal">
                               <h2 class="text-3xl md:text-5xl font-extrabold text-white">Готовы начать свой путь к гармонии?</h2>
                               <p class="mt-4 text-lg md:text-xl text-brand-secondary mb-10">
                                   Запустите AI-ассистента Vibes прямо сейчас в Telegram и начните управлять своей энергией осознанно.
                               </p>
                               <a href="https://t.me/myvibeappbot" target="_blank" class="bg-brand-accent text-white font-bold py-4 px-10 rounded-lg text-xl hover:bg-green-500 transition-transform transform hover:scale-105 inline-flex items-center">
                                   <svg class="w-6 h-6 mr-3" fill="currentColor" viewBox="0 0 24 24" aria-hidden="true"><path d="M9.78 18.65l.28-4.23 7.68-6.92c.34-.31-.07-.46-.52-.19L7.74 13.3 3.64 12c-.88-.25-.89-.86.2-1.3l15.97-6.16c.73-.33 1.43.18 1.15 1.3l-2.72 12.81c-.19.91-.74 1.13-1.51.71l-4.84-3.56-2.31 2.24c-.25.24-.45.44-.88.44l.21-4.58z"></path></svg>
                                   Перейти к боту
                               </a>
                           </div>
                       </section>
                   
                   </main>
                   
                   <!-- Footer (ИЗМЕНЕННЫЙ) -->
                   <footer class="py-8 px-4 border-t border-gray-800">
                       <div class="container mx-auto text-center text-brand-secondary">
                           <p>&copy; 2024 Vibes. Все права защищены. | vibes.nakodeelee.ru</p>
                           <!-- Добавлена ссылка на политику конфиденциальности -->
                           <p class="mt-2"><a href="https://vibes.nakodeelee.ru/privacy.html" class="underline hover:text-brand-primary">Политика конфиденциальности</a></p>
                       </div>
                   </footer>
                   
                   <!-- JavaScript for animations -->
                   <script>
                       document.addEventListener('DOMContentLoaded', function() {
                           // On-scroll reveal animation logic
                           const revealElements = document.querySelectorAll('.reveal');
                           const observer = new IntersectionObserver((entries) => {
                               entries.forEach(entry => {
                                   if (entry.isIntersecting) {
                                       entry.target.classList.add('visible');
                                       observer.unobserve(entry.target);
                                   }
                               });
                           }, {
                               threshold: 0.1
                           });
                           revealElements.forEach(el => {
                               observer.observe(el);
                           });
                       });
                   </script>
                   </body>
                   </html>
                   """;
        return new ContentResult
        {
            Content = html,
            ContentType = "text/html"
        };
    }
}