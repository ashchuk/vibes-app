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

                           /* Form focus styles */
                           .form-input:focus {
                               outline: none;
                               border-color: #58A6FF;
                               box-shadow: 0 0 0 3px rgba(88, 166, 255, 0.3);
                           }
                       </style>
                   </head>

                   <body class="bg-brand-bg text-brand-secondary antialiased">

                   <!-- Header -->
                   <header class="py-6 px-4 md:px-8 absolute top-0 left-0 w-full z-10">
                       <div class="container mx-auto flex justify-between items-center">
                           <h1 class="text-2xl font-bold text-white">Vibes</h1>
                           <a href="https://t.me/myvibeappbot" target="_blank" class="bg-brand-primary text-white font-semibold py-2 px-5 rounded-lg hover:bg-opacity-90 transition-colors">
                               Запустить ассистента
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

                       <!-- 2. Pains and Situations Section -->
                       <section id="pains" class="py-16 md:py-24 px-4">
                           <div class="container mx-auto max-w-5xl">
                               <div class="text-center mb-12 reveal">
                                   <h2 class="text-3xl md:text-4xl font-bold text-white">Знакомые ситуации?</h2>
                                   <p class="mt-2 text-lg">Vibes создан, чтобы помочь вам справиться с этим.</p>
                               </div>
                               <div class="grid grid-cols-1 md:grid-cols-2 gap-8">
                                   <!-- Pain Point 1 -->
                                   <div class="bg-brand-surface p-6 rounded-xl border border-gray-800 reveal">
                                       <h3 class="text-xl font-bold text-white mb-2">Выгорание и потеря фокуса</h3>
                                       <p>Постоянное чувство усталости и неспособность сконцентрироваться на важных задачах снижают вашу эффективность.</p>
                                   </div>
                                   <!-- Pain Point 2 -->
                                   <div class="bg-brand-surface p-6 rounded-xl border border-gray-800 reveal" style="transition-delay: 100ms;">
                                       <h3 class="text-xl font-bold text-white mb-2">Тревога и проблемы со сном</h3>
                                       <p>Беспокойные мысли мешают заснуть, а утро начинается с чувства разбитости, что портит весь день.</p>
                                   </div>
                                   <!-- Pain Point 3 -->
                                   <div class="bg-brand-surface p-6 rounded-xl border border-gray-800 reveal" style="transition-delay: 200ms;">
                                       <h3 class="text-xl font-bold text-white mb-2">Сложность в отслеживании прогресса</h3>
                                       <p>Вы работаете над собой, но без четкой системы сложно увидеть реальные изменения и сохранить мотивацию.</p>
                                   </div>
                                   <!-- Pain Point 4 -->
                                   <div class="bg-brand-surface p-6 rounded-xl border border-gray-800 reveal" style="transition-delay: 300ms;">
                                       <h3 class="text-xl font-bold text-white mb-2">Потеря связи с эмоциями</h3>
                                       <p>Вы не всегда понимаете причины своего настроения, что мешает вовремя реагировать на внутренние сигналы.</p>
                                   </div>
                               </div>
                           </div>
                       </section>

                       <!-- 3. About the Product Section -->
                       <section id="about" class="py-16 md:py-24 px-4 overflow-hidden">
                           <div class="container mx-auto max-w-6xl">
                               <div class="text-center mb-12 md:mb-20 reveal">
                                   <h2 class="text-3xl md:text-4xl font-bold text-white">Как это работает?</h2>
                                   <p class="mt-2 text-lg">Простой путь к осознанности и контролю.</p>
                               </div>
                               <div class="relative grid grid-cols-1 lg:grid-cols-3 items-center gap-8 md:gap-16">
                                   <!-- Left Side Steps -->
                                   <div class="space-y-8 md:space-y-12 text-center lg:text-right">
                                       <div class="reveal">
                                           <h3 class="text-2xl font-bold text-brand-primary">Шаг 01</h3>
                                           <p class="text-lg font-semibold text-white mt-1">Ежедневная рефлексия</p>
                                           <p class="mt-1">Всего 5 минут в день, чтобы отметить свое настроение, энергию и ключевые события.</p>
                                       </div>
                                       <div class="reveal">
                                           <h3 class="text-2xl font-bold text-brand-primary">Шаг 02</h3>
                                           <p class="text-lg font-semibold text-white mt-1">Анализ данных</p>
                                           <p class="mt-1">Vibes анализирует ваши записи и выявляет скрытые закономерности между вашими действиями и состоянием.</p>
                                       </div>
                                   </div>
                                   
                                   <!-- Right Side Steps -->
                                   <div class="space-y-8 md:space-y-12 text-center lg:text-left">
                                       <div class="reveal">
                                           <h3 class="text-2xl font-bold text-brand-primary">Шаг 03</h3>
                                           <p class="text-lg font-semibold text-white mt-1">Персональные инсайты</p>
                                           <p class="mt-1">Получайте персональные рекомендации и инсайты, которые помогут вам улучшить самочувствие.</p>
                                       </div>
                                       <div class="reveal">
                                           <h3 class="text-2xl font-bold text-brand-accent">Результат</h3>
                                           <p class="text-lg font-semibold text-white mt-1">Рост и гармония</p>
                                           <p class="mt-1">Осознанно управляйте своим состоянием, повышайте продуктивность и живите в гармонии с собой.</p>
                                       </div>
                                   </div>
                               </div>
                           </div>
                       </section>

                       <!-- 4. Features Section -->
                       <section id="features" class="py-16 md:py-24 px-4 bg-brand-surface border-y border-gray-800">
                           <div class="container mx-auto max-w-6xl">
                               <div class="text-center mb-12 reveal">
                                   <h2 class="text-3xl md:text-4xl font-bold text-white">Ключевые возможности</h2>
                                   <p class="mt-2 text-lg">Все инструменты для вашего благополучия в одном месте.</p>
                               </div>
                               <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-8">
                                   <!-- Feature 1 -->
                                   <div class="p-6 text-center border border-gray-700 rounded-xl hover:border-brand-primary hover:bg-gray-800/20 transition-all transform hover:-translate-y-1 reveal">
                                       <div class="flex items-center justify-center h-16 w-16 rounded-full bg-brand-primary/10 mx-auto mb-4">
                                           <!-- Placeholder for Icon -->
                                           <svg class="w-8 h-8 text-brand-primary" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M14.828 14.828a4 4 0 01-5.656 0M9 10h.01M15 10h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"></path></svg>
                                       </div>
                                       <h3 class="text-xl font-bold text-white">Трекер настроения</h3>
                                       <p class="mt-2">Простой и наглядный способ фиксировать эмоциональные взлеты и падения в течение дня.</p>
                                   </div>
                                   <!-- Feature 2 -->
                                   <div class="p-6 text-center border border-gray-700 rounded-xl hover:border-brand-primary hover:bg-gray-800/20 transition-all transform hover:-translate-y-1 reveal" style="transition-delay: 100ms;">
                                       <div class="flex items-center justify-center h-16 w-16 rounded-full bg-brand-primary/10 mx-auto mb-4">
                                           <svg class="w-8 h-8 text-brand-primary" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 6.253v11.494m-5.747-5.747h11.494"></path><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4.75 3.75l1.5 1.5M17.75 3.75l-1.5 1.5M4.75 20.25l1.5-1.5M17.75 20.25l-1.5-1.5"></path><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 6V4M6 12H4M12 18v-2M18 12h-2"></path></svg>
                                       </div>
                                       <h3 class="text-xl font-bold text-white">Дневник благодарности</h3>
                                       <p class="mt-2">Практика, которая помогает сфокусироваться на позитивных моментах и улучшить общее самочувствие.</p>
                                   </div>
                                   <!-- Feature 3 -->
                                   <div class="p-6 text-center border border-gray-700 rounded-xl hover:border-brand-primary hover:bg-gray-800/20 transition-all transform hover:-translate-y-1 reveal" style="transition-delay: 200ms;">
                                       <div class="flex items-center justify-center h-16 w-16 rounded-full bg-brand-primary/10 mx-auto mb-4">
                                           <svg class="w-8 h-8 text-brand-primary" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z"></path></svg>
                                       </div>
                                       <h3 class="text-xl font-bold text-white">Аналитика продуктивности</h3>
                                       <p class="mt-2">Отслеживайте, как ваше настроение и привычки влияют на вашу способность выполнять задачи.</p>
                                   </div>
                                   <!-- Feature 4 -->
                                   <div class="p-6 text-center border border-gray-700 rounded-xl hover:border-brand-primary hover:bg-gray-800/20 transition-all transform hover:-translate-y-1 reveal" style="transition-delay: 300ms;">
                                       <div class="flex items-center justify-center h-16 w-16 rounded-full bg-brand-primary/10 mx-auto mb-4">
                                           <svg class="w-8 h-8 text-brand-primary" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5.636 18.364a9 9 0 010-12.728m12.728 0a9 9 0 010 12.728m-9.9-1.414v.001M9 12h.01M15 12h.01M12 15h.01"></path></svg>
                                       </div>
                                       <h3 class="text-xl font-bold text-white">Медитации и дыхание</h3>
                                       <p class="mt-2">Библиотека практик для снятия стресса, улучшения концентрации и глубокого расслабления.</p>
                                   </div>
                               </div>
                           </div>
                       </section>

                       <!-- 5. Call To Action Section -->
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

                   <!-- Footer -->
                   <footer class="py-8 px-4 border-t border-gray-800">
                       <div class="container mx-auto text-center text-brand-secondary">
                           <p>&copy; 2024 Vibes. Все права защищены. | vibes.nakodeelee.ru</p>
                           <p><a href="https://vibes.nakodeelee.ru/privacy.html">Privacy Policy.</a></p>
                       </div>
                   </footer>


                   <!-- JavaScript for animations and form validation -->
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

                           // Client-side form validation logic
                           const form = document.getElementById('cta-form');
                           const feedbackEl = document.getElementById('form-feedback');

                           form.addEventListener('submit', function(event) {
                               event.preventDefault(); // Prevent actual submission

                               if (form.checkValidity()) {
                                   feedbackEl.textContent = 'Спасибо! Мы свяжемся с вами в ближайшее время.';
                                   feedbackEl.style.color = '#3FB950'; // brand-accent
                                   form.reset();
                               } else {
                                   feedbackEl.textContent = 'Пожалуйста, заполните обязательные поля (имя и email).';
                                   feedbackEl.style.color = '#F87171'; // red-400
                               }
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