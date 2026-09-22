import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';

interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  userId: number;
  email: string;
  role: string;
  token: string;
}

export interface SignupRequest {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {

  private apiUrl = `${environment.apiUrl}/api/auth`;

  // Used by Navbar to know whether user is logged in
  isLoggedIn = signal<boolean>(this.hasToken());

  constructor(private http: HttpClient) { }

  login(
    email: string,
    password: string
  ): Observable<LoginResponse> {

    const request: LoginRequest = {
      email,
      password
    };

    return this.http
      .post<LoginResponse>(
        `${this.apiUrl}/login`,
        request
      )
      .pipe(
        tap(response => {

          localStorage.setItem(
            'token',
            response.token
          );

          localStorage.setItem(
            'userId',
            response.userId.toString()
          );

          localStorage.setItem(
            'email',
            response.email
          );

          localStorage.setItem(
            'role',
            response.role
          );

          this.isLoggedIn.set(true);
        })
      );
  }

  signup(
    request: SignupRequest
  ): Observable<any> {

    return this.http.post(
      `${this.apiUrl}/register`,
      request
    );
  }

  getToken(): string | null {
    return localStorage.getItem('token');
  }

  getRole(): string | null {
    return localStorage.getItem('role');
  }

  getEmail(): string | null {
    return localStorage.getItem('email');
  }

  logout(): void {

    localStorage.removeItem('token');
    localStorage.removeItem('userId');
    localStorage.removeItem('email');
    localStorage.removeItem('role');

    this.isLoggedIn.set(false);
  }

  private hasToken(): boolean {
    return typeof localStorage !== 'undefined'
      && !!localStorage.getItem('token');
  }
}
