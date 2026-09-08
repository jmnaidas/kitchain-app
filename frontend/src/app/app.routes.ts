import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    title: 'Kitchain — Your pickleball companion.',
    loadComponent: () => import('./features/home/home').then((m) => m.Home),
  },
  {
    path: 'courts',
    title: 'Courts | Kitchain',
    loadComponent: () => import('./features/courts/courts').then((m) => m.Courts),
  },
  {
    path: 'courts/add',
    title: 'Add a Court | Kitchain',
    loadComponent: () => import('./features/courts/pages/court-add').then((m) => m.CourtAdd),
  },
  {
    path: 'courts/:id',
    title: 'Court details | Kitchain',
    loadComponent: () =>
      import('./features/courts/pages/court-details').then((m) => m.CourtDetails),
  },
  {
    path: 'admin/court-submissions',
    title: 'Submission review queue | Kitchain',
    loadComponent: () =>
      import('./features/courts/admin/court-submissions').then((m) => m.CourtSubmissions),
  },
  {
    path: 'admin/court-submissions/:id',
    title: 'Review submission | Kitchain',
    loadComponent: () =>
      import('./features/courts/admin/court-submission-review').then(
        (m) => m.CourtSubmissionReview,
      ),
  },
  {
    path: 'play',
    title: 'Play | Kitchain',
    loadComponent: () => import('./features/play/play').then((m) => m.Play),
  },
  {
    path: 'gear',
    title: 'Gear | Kitchain',
    loadComponent: () => import('./features/gear/gear').then((m) => m.Gear),
  },
  { path: '**', redirectTo: '' },
];
